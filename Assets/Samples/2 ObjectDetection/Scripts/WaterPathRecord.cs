using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class WaterPathRecord : MonoBehaviour
{
    // 定义数据结构：Unix时间戳(毫秒) + 坐标
    private struct PoseData
    {
        public long timestamp; // 使用 long 类型记录时间戳
        public Vector3 position;
    }

    private List<PoseData> trajectoryData = new List<PoseData>();
    private bool isRecording = false;

    public void StartRecording()
    {
        trajectoryData.Clear();
        isRecording = true;
        Debug.Log("开始记录轨迹数据（时间戳模式）...");
    }

    public void StopRecording()
    {
        isRecording = false;
        SaveToTxt();
    }

    void Update()
    {
        if (isRecording)
        {
            // 获取当前的 Unix 时间戳（毫秒）
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            PoseData data = new PoseData
            {
                timestamp = currentTimestamp,
                position = transform.position
            };
            trajectoryData.Add(data);
        }
    }

    private void SaveToTxt()
    {
        // 文件名依然包含易读的时间，方便你找文件
        string fileName = "WaterMelonRightHandRawTrack_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // 写入数据：时间戳 X Y Z
                foreach (var data in trajectoryData)
                {
                    // 使用空格分隔，最简单直接
                    writer.WriteLine($"{data.timestamp} {data.position.x:F4} {data.position.y:F4} {data.position.z:F4}");
                }
            }
            Debug.Log($"数据已存至: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("保存失败: " + e.Message);
        }
    }
}
