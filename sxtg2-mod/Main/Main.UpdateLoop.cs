using System;
using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Hooks.Audio;

namespace sxtg2
{
    public partial class Main
    {
        public override void OnUpdate()
        {
            try
            {
                // 씬 감지 모드가 활성화되어 있으면 주기적으로 체크
                _sceneDetector?.Update();

                // BGA와 BGM의 동기화 체크
                BGABGMSyncHook.CheckAndSync();
            }
            catch (Exception ex)
            {
                ModLog.Exception("Main.OnUpdate (씬/동기화)", ex);
            }
        }

        public override void OnApplicationQuit()
        {
            _sceneDetector?.Cleanup();
        }
    }
}
