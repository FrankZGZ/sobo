using UnityEngine;
using TMPro;

public class MarkerController : MonoBehaviour
{
    // 1. 定义三档模式的枚举
    public enum DisplayMode { None, Cube, Realistic }
    [Header("视觉物体引用")]
    [SerializeField] private GameObject defaultCube;   // 拖入红色方块
    [SerializeField] private GameObject bottleModel;  // 拖入水瓶模型
    private TextMeshProUGUI _textMesh;
    public float lastUpdateTime;



    private void Awake()
    {
        _textMesh = GetComponentInChildren<TextMeshProUGUI>();
        if (_textMesh == null)
        {
            Debug.LogError("No TextMeshProUGUI found on marker prefab!");
        }
    }
    public void SetVisual(YOLOv9Labels label, int mode)
    {
        if (defaultCube) defaultCube.SetActive(false);
        if (bottleModel) bottleModel.SetActive(false);

        if (mode == 1) // 红色方块模式
        {
            if (defaultCube) defaultCube.SetActive(true);
        }
        else if (mode == 2) // 对应模型模式
        {
            if (label == YOLOv9Labels.bottle && bottleModel)
                bottleModel.SetActive(true);
            else
                if (defaultCube) defaultCube.SetActive(true); // 没模型时用方块保底
        }
    }


    /// <summary>
    /// Updates the marker’s transform and text, and records the update time.
    /// </summary>
    public void UpdateMarker(Vector3 position, Quaternion rotation, Vector3 scale, string text)
    {
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = scale;
        if (_textMesh)
        {
            _textMesh.text = text;
        }
        
        lastUpdateTime = Time.time;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }
 
    private void Update()
    {
        if (gameObject.activeSelf && Time.time - lastUpdateTime > 2f)
        {
            gameObject.SetActive(false);
        }
    }
}
