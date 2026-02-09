using UnityEngine;

public class DetectionSystemManager : MonoBehaviour
{
    public GameObject detectionObject; // 拖入层级中的 ObjectDetection 物体
    public ObjectRenderer rendererScript; // 拖入 ObjectRenderer 脚本
    public GameObject menuToHide;
    public void ChooseMode(int mode)
    {
        if (mode == 0)
        {
            // 彻底关闭，ObjectDetector 脚本也就不会运行了
            detectionObject.SetActive(false);
        }
        else
        {
            // 设置模式并开启检测
            rendererScript.selectedMode = mode;
            detectionObject.SetActive(true);
        }

        if (menuToHide != null)
        {
            menuToHide.SetActive(false);
        }
        else
        {
            // 如果没指定 menuToHide，也不要报错
            Debug.LogWarning("未指定 menuToHide，菜单将保持显示。");
        }
    }
}