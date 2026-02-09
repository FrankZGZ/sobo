using UnityEngine;

public class GameSwitcher : MonoBehaviour
{
    [Header("大盒子引用")]
    public GameObject menuGroup;
    public GameObject watermelonGroup;
    public GameObject saberGroup;
    public GameObject saberModel;
    void Start()
    {
        // 游戏一开始，显示菜单，关掉所有游戏
        BackToMenu();
    }

    // 点击“切西瓜”按钮时运行
    public void EnterWatermelon()
    {
        menuGroup.SetActive(false);      // 藏起菜单
        watermelonGroup.SetActive(true); // 开启西瓜游戏
        saberGroup.SetActive(false);     // 关掉光剑游戏组
    }

    // 点击“光剑游戏”按钮时运行
    public void EnterSaber()
    {
        menuGroup.SetActive(false);
        watermelonGroup.SetActive(false);
        saberGroup.SetActive(true);      // 开启光剑游戏组
        saberModel.SetActive(true);
        // 注意：SaberGameManager 在激活时会自动运行其 Start() 里的逻辑
    }

    // 返回菜单（可以在游戏结束按钮上绑定这个）
    public void BackToMenu()
    {

        menuGroup.SetActive(true);
        watermelonGroup.SetActive(false);
        saberGroup.SetActive(false);
        saberModel.SetActive(false);
    }
}