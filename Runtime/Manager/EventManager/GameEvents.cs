namespace RuriKit
{
    /// <summary>
    ///     定义游戏流程和设置界面使用的全局事件标识。
    /// </summary>
    public enum GameEvents
    {
        // --------------------------------------  Game  --------------------------------------------

        OnScoreHasChanged, //  当玩家得分更新
        OnIntoBackSetting, //  当进入后台设置界面
        OnExitBackSetting, //  当退出后台设置界面

        // --------------------------------------  Port  --------------------------------------------

        OnPlayerHasGoal, //    当玩家进球时
        OnClearPlayerCoin, //  当清除玩家未玩的币数和局数时

        OnPlayerCoinIn, //  <int>              当玩家投币时，参数为投币数
        OnMachineError //   <MachineErrorCode> 当机器报错时
    }
}