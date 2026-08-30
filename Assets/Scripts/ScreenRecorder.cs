using Assets.Scripts.Types;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using UnityEngine;
using UnityEngine.UI;

public class ScreenRecorder : MonoBehaviour
{
    private const float FfmpegExitTimeoutSeconds = 10f;
    private const float FfmpegKillTimeoutSeconds = 2f;

    public float CutoffTime;
    public GameObject APObj;
    JsonDataLoader loader;
    ObjectCounter counter;
    AudioTimeProvider timeProvider;

    private bool isRecording;
    private bool cutoffInitialized;
    private bool recordingTimedOut;
    private string timeoutMessage = string.Empty;

    // Start is called before the first frame update
    private void Start()
    {
        loader = FindAnyObjectByType<JsonDataLoader>();
        counter = FindAnyObjectByType<ObjectCounter>();
        timeProvider = FindAnyObjectByType<AudioTimeProvider>();
    }

    // Update is called once per frame
    private void Update()
    {
        if (!isRecording)
            return;

        if (!cutoffInitialized && loader.ChartEndTime.HasValue)
        {
            CutoffTime = RecordingTimeoutPolicy.CalculateCutoffTime(loader.ChartEndTime.Value);
            cutoffInitialized = true;
            print($"recording cutoff initialized: chart={loader.ChartEndTime.Value:F3}s, cutoff={CutoffTime:F3}s");
        }

        if (loader.State == NoteLoaderStatus.Finished && counter.AllFinished && APObj == null)
        {
            isRecording = false;
            return;
        }

        if (cutoffInitialized && RecordingTimeoutPolicy.HasReachedCutoff(timeProvider.AudioTime, CutoffTime))
            StopRecordingForTimeout();
    }

    public void StartRecording(string maidata_path)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
        {
            const string unsupportedMessage =
                "录制功能目前仅支持 Windows 平台，已忽略本次录制请求。\n" +
                "Recording is currently only supported on Windows; the request was ignored.";
            UnityEngine.Debug.LogWarning(unsupportedMessage);
            var errObject = GameObject.Find("ErrText");
            if (errObject != null)
            {
                var errText = errObject.GetComponent<Text>();
                if (errText != null)
                    errText.text += unsupportedMessage + "\n";
            }
            return;
        }

        CutoffTime = 0f;
        cutoffInitialized = false;
        recordingTimedOut = false;
        timeoutMessage = string.Empty;
        StartCoroutine(CaptureScreen(maidata_path));
    }

    public void StopRecording()
    {
        print("stop recording");
        isRecording = false;
    }

    private void StopRecordingForTimeout()
    {
        recordingTimedOut = true;
        timeoutMessage =
            $"录制达到截止时间，已停止送帧。AudioTime={timeProvider.AudioTime:F3}s, CutoffTime={CutoffTime:F3}s, " +
            RecordingTimeoutPolicy.FormatProgress(
                counter.tapCount,
                counter.tapSum,
                counter.holdCount,
                counter.holdSum,
                counter.slideCount,
                counter.slideSum,
                counter.touchCount,
                counter.touchSum,
                counter.breakCount,
                counter.breakSum,
                counter.mineCount,
                counter.mineSum) +
            $", AP {(APObj == null ? "finished" : "pending")}";
        UnityEngine.Debug.LogWarning(timeoutMessage);
        isRecording = false;
    }

    private IEnumerator CaptureScreen(string maidata_path)
    {
        timeProvider = GameObject.Find("AudioTimeProvider").GetComponent<AudioTimeProvider>();
        var bgManager = GameObject.Find("Background").GetComponent<BGManager>();
        if (Screen.width % 2 != 0 || Screen.height % 2 != 0)
        {
            GameObject.Find("ErrText").GetComponent<Text>().text =
                "无法开始编码，因为分辨率宽度或高度不是偶数。\nCan not start render because the width/height is not even.\n当前分辨率:" +
                Screen.width + "x" + Screen.height + "\n";
            yield break;
        }

        if (File.Exists(maidata_path + "\\out.mp4"))
            File.Delete(maidata_path + "\\out.mp4");

        byte[] data;
        var texture = new Texture2D(0, 0);
        using (var pipeServer = new NamedPipeServerStream("majdataRec", PipeDirection.Out))
        {
            var wavpath = "out.wav";
            var outputfile = "out.mp4";

            var arguments = string.Format(
                File.ReadAllText(Application.streamingAssetsPath + "\\ffarguments.txt").Trim(),
                Screen.width, Screen.height,
                wavpath, outputfile,
                int.MaxValue
            );
            var startinfo = new ProcessStartInfo(Application.streamingAssetsPath + "\\ffmpeg.exe", arguments);
            startinfo.UseShellExecute = false;
            startinfo.CreateNoWindow = true;
            startinfo.WorkingDirectory = maidata_path;
            startinfo.EnvironmentVariables.Add("FFREPORT", "file=out.log:level=24");
            print(arguments);

            var p = Process.Start(startinfo);
            pipeServer.WaitForConnection();
            isRecording = true;
            using (var bw = new BinaryWriter(pipeServer))
            {
                do
                {
                    yield return new WaitForEndOfFrame();
                    try
                    {
                        texture.Reinitialize(0, 0);
                        texture = ScreenCapture.CaptureScreenshotAsTexture();
                        /*                    int width = texture.width;
                                            int height = texture.height;*/

                        data = texture.GetRawTextureData();

                        bw.Write(data, 0, data.Length);
                        bw.Flush();
                        //Thread.Sleep(100);
                    }
                    catch
                    {
                    }
                } while (
                    pipeServer.IsConnected &&
                    isRecording &&
                    !p.HasExited
                );
            }

            yield return WaitForFfmpegExit(p);

            var ffmpegExited = p.HasExited;
            var exitCode = ffmpegExited ? p.ExitCode : -1;
            var errText = GameObject.Find("ErrText").GetComponent<Text>();
            if (recordingTimedOut)
                errText.text += timeoutMessage + "\n";

            if (File.Exists(maidata_path + "/out.mp4") && exitCode == 0)
            {
                errText.text += "渲染成功，视频生成在" + maidata_path +
                                "\\out.mp4\nRender Successed\nExitCode:" +
                                exitCode;
                Process.Start("explorer", "/select,\"" + maidata_path + "\\out.mp4" + "\"");
            }
            else
            {
                errText.text += "编码器已退出\nFFmpeg Exited.\nExitCode:" + exitCode;
            }

            p.Dispose();
        }

        isRecording = false;
        timeProvider.isStart = false;
        Time.captureFramerate = 0;
        bgManager.PauseVideo();
    }

    private IEnumerator WaitForFfmpegExit(Process process)
    {
        var exitDeadline = Time.realtimeSinceStartup + FfmpegExitTimeoutSeconds;
        while (!process.HasExited && Time.realtimeSinceStartup < exitDeadline)
            yield return null;

        if (process.HasExited)
            yield break;

        UnityEngine.Debug.LogWarning(
            $"FFmpeg did not exit within {FfmpegExitTimeoutSeconds:F0}s after recording stopped; terminating it.");
        try
        {
            process.Kill();
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"Failed to terminate FFmpeg: {exception}");
            yield break;
        }

        var killDeadline = Time.realtimeSinceStartup + FfmpegKillTimeoutSeconds;
        while (!process.HasExited && Time.realtimeSinceStartup < killDeadline)
            yield return null;
    }
}
