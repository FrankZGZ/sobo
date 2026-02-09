using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Meta.XR;
using System.Net.Sockets;

public class ObjectRenderer : MonoBehaviour
{
    [Header("Controller References")]
    [Tooltip("将 OVRCameraRig 下的 LeftControllerAnchor 拖入此处")]
    [SerializeField] private Transform leftControllerAnchor;

    [Tooltip("将 OVRCameraRig 下的 RightControllerAnchor 拖入此处")]
    [SerializeField] private Transform rightControllerAnchor;

    [Header("Feedback Control (开关控制)")]
    [Tooltip("是否开启声音反馈")]
    public bool enableAudio = true; 
    [Tooltip("是否开启手柄震动")]
    public bool enableHaptics = true;
    [Tooltip("是否显示视觉Marker (Cube/文字)")]
    public bool enableVisual = true;

    [Header("Detection Alert")]
    [Tooltip("Cooldown between alerts (seconds)")]
    [SerializeField] private float alertCooldown = 0.5f;
    [Tooltip("AudioSource used for detection alert")]
    [SerializeField] private AudioSource alertAudioSource;
    [Tooltip("Optional alert clip; if null, AudioSource.clip is used")]
    [SerializeField] private AudioClip alertClip;
    [SerializeField, Range(0f, 1f)] private float alertVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float alertHapticsStrength = 0.6f;
    [SerializeField] private float alertHapticsDuration = 0.08f;
    [Tooltip("Enable continuous detection beep (pitch increases as distance gets closer)")]
    [SerializeField] private bool enableDetectionBeep = true;
    [SerializeField] private float beepMinPitch = 0.8f;
    [SerializeField] private float beepMaxPitch = 2.0f;
    [SerializeField] private float beepClipLength = 0.12f;
    [SerializeField] private float beepBaseFrequency = 880f;
    [SerializeField] private float beepMinInterval = 0.12f;
    [SerializeField] private float beepMaxInterval = 0.6f;

    [Header("Haptics Settings")]
    [Tooltip("触发震动和警告的距离阈值 (米)")]
    [SerializeField] private float alertThreshold = 0.3f; // 30cm 阈值
    [Tooltip("Haptics frequency (0~1). Higher = faster vibration.")]
    [SerializeField, Range(0f, 1f)] private float hapticsFrequency = 1f;
    [Tooltip("Pulse rate in Hz for haptics. Higher = faster pulses.")]
    [SerializeField] private float hapticsPulseHz = 8f;

    [Header("Voting Settings")]
    [Tooltip("Enable temporal voting to stabilize detections")]
    [SerializeField] private bool enableVoting = true;
    [Tooltip("Vote window length in seconds")]
    [SerializeField] private float voteWindowSeconds = 1.0f;
    [Tooltip("Minimum hits inside the window to keep showing")]
    [SerializeField] private int voteMinHits = 3;
    [Tooltip("Show duration after a hit (seconds)")]
    [SerializeField] private float showDurationSeconds = 1.0f;

    
    [Header("Camera & Raycast Settings")]
    [SerializeField] private float mergeThreshold = 0.2f;
    
    [Header("Marker Settings")]
    [SerializeField] private GameObject markerPrefab;
    
    [Header("Label Filtering")]
    [SerializeField] private YOLOv9Labels[] labelFilters;
    [SerializeField, Range(0f, 1f)] private float minConfidence = 0.15f;

    private Camera _mainCamera;
    private const float ModelInputSize = 640f;
    private PassthroughCameraAccess _cameraAccess;
    private EnvironmentRaycastManager _envRaycastManager;
    private readonly Dictionary<string, MarkerController> _activeMarkers = new();
    private float _nextAlertTime;
    private float _alertHapticsUntil;
    private float _alertHapticsStrength;
    private bool _hasDetections;
    private float _nearestDetectionDistance = float.PositiveInfinity;
    private float _nextBeepTime;
    private StreamWriter _frameLogWriter;
    private StreamWriter _labelLogWriter;
    private string _frameLogPath;
    private string _labelLogPath;
    private readonly List<TrackedDetection> _trackedDetections = new();
    
    // 用于控制显示的模式
    public int selectedMode = 1;

    private class TrackedDetection
    {
        public YOLOv9Labels Label;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public string LabelText;
        public float LastHitTime;
        public float ShowUntil;
        public readonly List<float> HitTimes = new();
    }


    private void Awake()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>() ?? FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
        _envRaycastManager = GetComponent<EnvironmentRaycastManager>() ?? FindAnyObjectByType<EnvironmentRaycastManager>(FindObjectsInactive.Include);
        if (alertAudioSource == null)
        {
            alertAudioSource = GetComponent<AudioSource>();
        }
        if (alertAudioSource != null)
        {
            alertAudioSource.loop = false;
        }
        if (!_cameraAccess || !_envRaycastManager)
        {
            Debug.LogWarning("[Detection3DRenderer] Passthrough camera or Environment Raycast Manager is not ready.");
            return;
        }
        _mainCamera = Camera.main;
        InitializeLogs();
    }

    private void Update()
    {
        // 实时处理震动 (受 enableHaptics 控制)
        HandleProximityHaptics();
        UpdateDetectionBeep();
        LogFrame();
    }

    /// <summary>
    /// 在每一帧计算手柄与所有活跃标记的距离，并应用线性震动
    /// </summary>
    private void HandleProximityHaptics()
    {
        // 如果没有开启震动功能，强制停止震动并退出
        if (!enableHaptics)
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            return;
        }

        float maxVibrationStrength = 0f;
        
        if (rightControllerAnchor != null)
        {
            // 遍历所有当前存在的识别物体
            foreach (var marker in _activeMarkers.Values)
            {
                if (marker == null) continue;

                // 计算当前手柄到该物体的实时距离
                float distance = Vector3.Distance(rightControllerAnchor.position, marker.transform.position);

                // 如果进入阈值范围 (例如 < 0.3m)
                if (distance < alertThreshold)
                {
                    // 线性计算强度：距离越近，强度越大 (0.0 ~ 1.0)
                    float strength = 1.0f - (distance / alertThreshold);
                    strength = Mathf.Clamp01(strength);

                    if (strength > maxVibrationStrength)
                    {
                        maxVibrationStrength = strength;
                    }
                }
            }
        }

        if (Time.time < _alertHapticsUntil)
        {
            maxVibrationStrength = Mathf.Max(maxVibrationStrength, _alertHapticsStrength);
        }

        if (maxVibrationStrength > 0.01f)
        {
            var pulseHz = Mathf.Max(0.1f, hapticsPulseHz);
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * pulseHz);
            var pulsedStrength = maxVibrationStrength * pulse;
            OVRInput.SetControllerVibration(hapticsFrequency, pulsedStrength, OVRInput.Controller.RTouch);
        }
        else
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }
    }
    
    public void RenderDetections(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<int> labelIDs, Unity.InferenceEngine.Tensor<float> confidences = null)
    {
        if (coords == null || labelIDs == null) return;
        if (!_cameraAccess || !_envRaycastManager) return;

        var numDetections = coords.shape[0];
        var now = Time.time;
        var renderedCount = 0;
        var nearestDistance = float.PositiveInfinity;

        var imageWidth = ModelInputSize;
        var imageHeight = ModelInputSize;
        var halfWidth = imageWidth * 0.5f;
        var halfHeight = imageHeight * 0.5f;

        for (var i = 0; i < numDetections; i++)
        {
            var detectedCenterX = coords[i, 0];
            var detectedCenterY = coords[i, 1];
            var detectedWidth = coords[i, 2];
            var detectedHeight = coords[i, 3];

            var adjustedCenterX = detectedCenterX - halfWidth;
            var adjustedCenterY = detectedCenterY - halfHeight;

            var perX = (adjustedCenterX + halfWidth) / imageWidth;
            var perY = (adjustedCenterY + halfHeight) / imageHeight;
            var centerRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(perX, perY));

            if (!_envRaycastManager.Raycast(centerRay, out var centerHit)) continue;

            var markerWorldPos = centerHit.point;

            var u1 = (detectedCenterX - detectedWidth * 0.5f) / imageWidth;
            var v1 = (detectedCenterY - detectedHeight * 0.5f) / imageHeight;
            var u2 = (detectedCenterX + detectedWidth * 0.5f) / imageWidth;
            var v2 = (detectedCenterY + detectedHeight * 0.5f) / imageHeight;

            var tlRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v1));
            var trRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v1));
            var blRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v2));
            var brRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v2));

            var depth = Vector3.Distance(_mainCamera.transform.position, markerWorldPos);
            var worldTL = tlRay.GetPoint(depth);
            var worldTR = trRay.GetPoint(depth);
            var worldBL = blRay.GetPoint(depth);

            var markerWidth = Vector3.Distance(worldTR, worldTL);
            var markerHeight = Vector3.Distance(worldBL, worldTL);
            var markerScale = new Vector3(markerWidth, markerHeight, 0.05f);

            var detectedLabel = (YOLOv9Labels)labelIDs[i];
            if (labelFilters is { Length: > 0 } && !Array.Exists(labelFilters, label => label == detectedLabel)) continue;

            var surfaceNormal = SampleSurfaceNormal(markerWorldPos, centerHit.normal);
            var markerRotation = Quaternion.LookRotation(-surfaceNormal, Vector3.up);

            var confidence = GetConfidence(coords, confidences, i);
            if (confidence >= 0f && confidence < minConfidence) continue;

            string rightHandDistStr = "N/A";
            float rightHandDist = 10000.0f;
            if (rightControllerAnchor != null)
            {
                rightHandDist = Vector3.Distance(markerWorldPos, rightControllerAnchor.position);
                rightHandDistStr = rightHandDist < 1f ? $"{(rightHandDist * 100):F1}cm" : $"{rightHandDist:F2}m";
            }

            var baseLabel = confidence >= 0f ? $"{detectedLabel} ({confidence * 100f:F0}%)" : detectedLabel.ToString();
            var labelWithConfidence = $"{baseLabel}\nSize: {markerWidth:F2}m x {markerHeight:F2}m" 
                + $"Hand Dist: {rightHandDistStr}";

            var tracked = FindTrackedDetection(detectedLabel, markerWorldPos);
            if (tracked == null)
            {
                tracked = new TrackedDetection { Label = detectedLabel };
                _trackedDetections.Add(tracked);
            }

            tracked.Position = markerWorldPos;
            tracked.Rotation = markerRotation;
            tracked.Scale = markerScale;
            tracked.LabelText = labelWithConfidence;
            tracked.LastHitTime = now;
            tracked.HitTimes.Add(now);
            tracked.ShowUntil = Mathf.Max(tracked.ShowUntil, now + Mathf.Max(0.01f, showDurationSeconds));
        }

        PruneTrackedDetections(now);

        ClearPreviousMarkers();

        foreach (var tracked in _trackedDetections)
        {
            var show = now <= tracked.ShowUntil;
            if (enableVoting)
            {
                var hits = CountHits(tracked, now);
                if (hits >= voteMinHits)
                {
                    tracked.ShowUntil = Mathf.Max(tracked.ShowUntil, now + Mathf.Max(0.01f, showDurationSeconds));
                    show = true;
                }
            }

            if (!show) continue;

            var markerGo = Instantiate(markerPrefab);
            markerGo.name = tracked.Label.ToString();
            var marker = markerGo.GetComponent<MarkerController>();
            if (!marker) continue;

            var markerAudio = markerGo.GetComponentInChildren<AudioSource>();
            if (markerAudio != null && !enableDetectionBeep)
            {
                if (enableAudio && rightControllerAnchor != null)
                {
                    var dist = Vector3.Distance(tracked.Position, rightControllerAnchor.position);
                    if (dist < alertThreshold)
                    {
                        float t = 1.0f - (dist / alertThreshold);
                        markerAudio.volume = Mathf.Clamp01(0.5f + (t * 0.5f));
                        if (!markerAudio.isPlaying) markerAudio.Play();
                    }
                    else if (markerAudio.isPlaying)
                    {
                        markerAudio.Stop();
                    }
                }
                else if (markerAudio.isPlaying)
                {
                    markerAudio.Stop();
                }
            }

            markerGo.SetActive(enableVisual);
            marker.SetVisual(tracked.Label, selectedMode);
            marker.UpdateMarker(tracked.Position, tracked.Rotation, tracked.Scale, tracked.LabelText);
            _activeMarkers[$"{tracked.Label}_{renderedCount}"] = marker;
            if (enableVisual) renderedCount++;

            if (rightControllerAnchor != null)
            {
                var dist = Vector3.Distance(tracked.Position, rightControllerAnchor.position);
                if (dist < nearestDistance) nearestDistance = dist;
            }
        }

        if (renderedCount > 0)
        {
            TriggerDetectionAlert();
        }

        _hasDetections = renderedCount > 0;
        _nearestDetectionDistance = nearestDistance;
    }

    private void ClearPreviousMarkers()
    {
        foreach (var marker in _activeMarkers.Values)
        {
            if (marker && marker.gameObject)
            {
                Destroy(marker.gameObject);
            }
        }
        _activeMarkers.Clear();
    }

    private TrackedDetection FindTrackedDetection(YOLOv9Labels label, Vector3 position)
    {
        TrackedDetection best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var tracked in _trackedDetections)
        {
            if (tracked.Label != label) continue;
            var dist = Vector3.Distance(tracked.Position, position);
            if (dist < mergeThreshold && dist < bestDistance)
            {
                best = tracked;
                bestDistance = dist;
            }
        }
        return best;
    }

    private void PruneTrackedDetections(float now)
    {
        var window = Mathf.Max(0.05f, voteWindowSeconds);
        for (int i = _trackedDetections.Count - 1; i >= 0; i--)
        {
            var tracked = _trackedDetections[i];
            PruneHitTimes(tracked, now, window);
            if (tracked.HitTimes.Count == 0 && now > tracked.ShowUntil)
            {
                _trackedDetections.RemoveAt(i);
            }
        }
    }

    private int CountHits(TrackedDetection tracked, float now)
    {
        var window = Mathf.Max(0.05f, voteWindowSeconds);
        PruneHitTimes(tracked, now, window);
        return tracked.HitTimes.Count;
    }

    private void PruneHitTimes(TrackedDetection tracked, float now, float window)
    {
        for (int i = tracked.HitTimes.Count - 1; i >= 0; i--)
        {
            if (tracked.HitTimes[i] < now - window)
            {
                tracked.HitTimes.RemoveAt(i);
            }
        }
    }


    // --- 下面是工具函数，保持不变 ---
    private Vector2 DetectionToViewport(float normalizedX, float normalizedY)
    {
        var resolution = (Vector2)_cameraAccess.CurrentResolution;
        if (resolution == Vector2.zero) resolution = (Vector2)_cameraAccess.Intrinsics.SensorResolution;
        if (resolution == Vector2.zero) return new Vector2(Mathf.Clamp01(normalizedX), Mathf.Clamp01(1f - normalizedY));

        var scaledX = Mathf.Clamp01(normalizedX) * ModelInputSize;
        var scaledY = Mathf.Clamp01(normalizedY) * ModelInputSize;
        var actualPixel = new Vector2(scaledX * (resolution.x / ModelInputSize), scaledY * (resolution.y / ModelInputSize));
        return new Vector2(Mathf.Clamp01(actualPixel.x / resolution.x), Mathf.Clamp01(1f - actualPixel.y / resolution.y));
    }

    private static float GetConfidence(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<float> confidenceTensor, int index)
    {
        var sampled = SampleConfidence(confidenceTensor, index);
        if (sampled >= 0f) return Mathf.Clamp01(sampled);
        if (coords == null || coords.shape.rank < 2) return -1f;
        var channels = coords.shape[coords.shape.rank - 1];
        if (channels <= 4) return -1f;
        try { return Mathf.Clamp01(coords[index, 4]); } catch { return -1f; }
    }

    private static float SampleConfidence(Unity.InferenceEngine.Tensor<float> tensor, int index)
    {
        if (tensor == null) return -1f;
        var length = tensor.shape.length;
        if (index < 0 || index >= length) return -1f;
        try { return tensor[index]; } catch { return -1f; }
    }

    private Vector3 SampleSurfaceNormal(Vector3 position, Vector3 fallbackNormal)
    {
        if (_envRaycastManager == null) return fallbackNormal;
        var origin = _mainCamera ? _mainCamera.transform.position : position - fallbackNormal * 0.1f;
        var direction = position - origin;
        if (direction.sqrMagnitude > 0.0001f)
        {
            if (_envRaycastManager.Raycast(new Ray(origin, direction.normalized), out var hit, direction.magnitude + 0.05f)) return hit.normal;
        }
        var offsetOrigin = position + fallbackNormal.normalized * 0.05f;
        if (_envRaycastManager.Raycast(new Ray(offsetOrigin, -fallbackNormal.normalized), out var reverseHit, 0.2f)) return reverseHit.normal;
        return fallbackNormal;
    }

    private void TriggerDetectionAlert()
    {
        if (Time.time < _nextAlertTime) return;
        _nextAlertTime = Time.time + alertCooldown;

        if (enableHaptics)
        {
            _alertHapticsUntil = Time.time + alertHapticsDuration;
            _alertHapticsStrength = Mathf.Clamp01(alertHapticsStrength);
        }
    }

    private void UpdateDetectionBeep()
    {
        if (!enableAudio || !enableDetectionBeep || alertAudioSource == null)
        {
            StopDetectionBeep();
            return;
        }

        if (!_hasDetections || rightControllerAnchor == null || float.IsInfinity(_nearestDetectionDistance))
        {
            StopDetectionBeep();
            return;
        }

        var maxDistance = Mathf.Max(0.01f, alertThreshold);
        if (_nearestDetectionDistance > maxDistance)
        {
            StopDetectionBeep();
            return;
        }

        EnsureBeepClip();

        var t = 1.0f - Mathf.Clamp01(_nearestDetectionDistance / maxDistance);
        var pitch = Mathf.Lerp(beepMinPitch, beepMaxPitch, t);
        alertAudioSource.pitch = pitch;
        alertAudioSource.volume = alertVolume;

        var minInterval = Mathf.Max(0.02f, beepMinInterval);
        var maxInterval = Mathf.Max(minInterval, beepMaxInterval);
        var interval = Mathf.Lerp(maxInterval, minInterval, t);

        if (Time.time >= _nextBeepTime)
        {
            alertAudioSource.PlayOneShot(alertAudioSource.clip, alertVolume);
            _nextBeepTime = Time.time + interval;
        }
    }

    private void StopDetectionBeep()
    {
        if (alertAudioSource != null)
        {
            if (alertAudioSource.isPlaying) alertAudioSource.Stop();
            _nextBeepTime = 0f;
        }
    }

    private void EnsureBeepClip()
    {
        if (alertClip != null)
        {
            if (alertAudioSource.clip != alertClip) alertAudioSource.clip = alertClip;
            return;
        }

        if (alertAudioSource.clip == null)
        {
            alertAudioSource.clip = CreateBeepClip(beepBaseFrequency, beepClipLength);
        }
    }

    private AudioClip CreateBeepClip(float frequency, float lengthSeconds)
    {
        var sampleRate = 44100;
        var lengthSamples = Mathf.Max(1, Mathf.CeilToInt(sampleRate * Mathf.Max(0.02f, lengthSeconds)));
        var samples = new float[lengthSamples];
        var twoPi = Mathf.PI * 2f;
        var fadeSamples = Mathf.Min(lengthSamples / 10, 200);

        for (var i = 0; i < lengthSamples; i++)
        {
            var t = (float)i / sampleRate;
            var sample = Mathf.Sin(twoPi * frequency * t);

            if (i < fadeSamples)
            {
                sample *= (float)i / fadeSamples;
            }
            else if (i > lengthSamples - fadeSamples - 1)
            {
                sample *= (float)(lengthSamples - 1 - i) / fadeSamples;
            }

            samples[i] = sample * 0.6f;
        }

        var clip = AudioClip.Create("DetectionBeep", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void InitializeLogs()
    {
        if (_frameLogWriter != null || _labelLogWriter != null) return;

        var dir = Application.persistentDataPath;
        _frameLogPath = Path.Combine(dir, "object_detection_frames.csv");
        _labelLogPath = Path.Combine(dir, "object_detection_labels.csv");

        _frameLogWriter = new StreamWriter(_frameLogPath, true);
        _labelLogWriter = new StreamWriter(_labelLogPath, true);

        if (new FileInfo(_frameLogPath).Length == 0)
        {
            _frameLogWriter.WriteLine("timestamp_iso,time_seconds,hand_x,hand_y,hand_z,item_x,item_y,item_z,visible");
        }

        if (new FileInfo(_labelLogPath).Length == 0)
        {
            _labelLogWriter.WriteLine("timestamp_iso,time_seconds,label,colliding");
        }

        _frameLogWriter.Flush();
        _labelLogWriter.Flush();
    }

    private void LogFrame()
    {
        if (_frameLogWriter == null || _labelLogWriter == null) return;

        var nowIso = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var now = Time.time;

        var handPos = rightControllerAnchor != null ? rightControllerAnchor.position : Vector3.zero;
        var hasHand = rightControllerAnchor != null;

        var visible = 0;
        var nearestPos = Vector3.zero;
        var hasItem = false;
        var nearestDist = float.PositiveInfinity;

        foreach (var kvp in _activeMarkers)
        {
            var marker = kvp.Value;
            if (marker == null || marker.gameObject == null) continue;
            if (!marker.gameObject.activeSelf) continue;
            visible = 1;

            var pos = marker.transform.position;
            if (!hasItem)
            {
                nearestPos = pos;
                hasItem = true;
            }

            if (hasHand)
            {
                var dist = Vector3.Distance(handPos, pos);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos = pos;
                }
            }
        }

        var frameLine = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1:F4},{2},{3},{4},{5},{6},{7},{8}",
            nowIso,
            now,
            hasHand ? handPos.x.ToString("F4", CultureInfo.InvariantCulture) : "",
            hasHand ? handPos.y.ToString("F4", CultureInfo.InvariantCulture) : "",
            hasHand ? handPos.z.ToString("F4", CultureInfo.InvariantCulture) : "",
            hasItem ? nearestPos.x.ToString("F4", CultureInfo.InvariantCulture) : "",
            hasItem ? nearestPos.y.ToString("F4", CultureInfo.InvariantCulture) : "",
            hasItem ? nearestPos.z.ToString("F4", CultureInfo.InvariantCulture) : "",
            visible);
        _frameLogWriter.WriteLine(frameLine);

        var wroteLabel = false;
        foreach (var kvp in _activeMarkers)
        {
            var marker = kvp.Value;
            if (marker == null || marker.gameObject == null) continue;
            if (!marker.gameObject.activeSelf) continue;

            var label = marker.gameObject.name;
            var colliding = 0;
            if (hasHand)
            {
                var dist = Vector3.Distance(handPos, marker.transform.position);
                if (dist < alertThreshold) colliding = 1;
            }

            var labelLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1:F4},{2},{3}",
                nowIso,
                now,
                label,
                colliding);
            _labelLogWriter.WriteLine(labelLine);
            wroteLabel = true;
        }

        if (!wroteLabel)
        {
            var emptyLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1:F4},,{2}",
                nowIso,
                now,
                0);
            _labelLogWriter.WriteLine(emptyLine);
        }

        _frameLogWriter.Flush();
        _labelLogWriter.Flush();
    }

    private void OnDestroy()
    {
        if (_frameLogWriter != null)
        {
            _frameLogWriter.Flush();
            _frameLogWriter.Dispose();
            _frameLogWriter = null;
        }

        if (_labelLogWriter != null)
        {
            _labelLogWriter.Flush();
            _labelLogWriter.Dispose();
            _labelLogWriter = null;
        }
    }
}
