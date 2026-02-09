using UnityEngine;

public class SaberTarget : MonoBehaviour
{
    public float speed = 3.0f; // 向玩家飞行的速度
    [Header("切割设定")]
    // 水平方块预制体勾选此项，竖直方块不勾选
    public bool requiredHorizontal;
    private Vector3 entryPosition;  // 记录切入点
    private bool wasHit = false;
    void Update()
    {
        // 核心逻辑：让方块一直向着玩家飞（Z轴负方向）
        transform.Translate(Vector3.back * speed * Time.deltaTime);

        // 漏切判定：飞过头了（Z < -1.0）
        if (!wasHit && transform.position.z < -1.0f)
        {
            wasHit = true;
            SaberGameManager.Instance.missCount++;
            Destroy(gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Saber"))
        {
            // 记录进入瞬间的光剑（通常是手部位置）坐标
            entryPosition = other.transform.position;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (wasHit || !other.CompareTag("Saber")) return;

        wasHit = true;

        // 计算切割矢量
        Vector3 sliceDirection = other.transform.position - entryPosition;

        // 判定原理：比较 X 和 Y 轴的移动量大小
        // $|x| > |y|$ 为水平切，反之为竖直切
        bool isHorizontalSlice = Mathf.Abs(sliceDirection.x) > Mathf.Abs(sliceDirection.y);

        // 与预设的方向对比
        bool isCorrect = (isHorizontalSlice == requiredHorizontal);

        SaberGameManager.Instance.RegisterHit(isCorrect);
        Destroy(gameObject);
    }
    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other.CompareTag("Saber"))
    //    {
    //        // 加分逻辑
    //        if (SaberGameManager.Instance != null)
    //        {
    //            SaberGameManager.Instance.AddScore(10); // 切中一个方块加10分
    //        }

    //        // 如果你用的是 Meta XR SDK，可以加震动
    //        //OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);

    //        Destroy(gameObject);
    //    }
    //}
}