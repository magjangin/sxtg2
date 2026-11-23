using System;
using System.IO;
using System.Text;

namespace sxtg2.Helpers
{
    /// <summary>
    /// Unity·MelonLoader에 의존하지 않는 설정/파일 도우미. 로직 테스트(sxtg2.LogicTests)가 이 파일을 그대로 링크해 검사한다.
    /// </summary>
    public static class ConfigValueParser
    {
        public static bool ParseFlexibleBool(string val, bool defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            var s = val.Trim().ToLowerInvariant();

            if (s == "1" || s == "true" || s == "t" || s == "on" || s == "켜짐" || s == "사용" || s == "활성화" || s == "enable" || s == "enabled" || s == "yes" || s == "y" || s == "트루" || s == "참" || s == "켜기")
                return true;

            if (s == "0" || s == "false" || s == "f" || s == "off" || s == "꺼짐" || s == "미사용" || s == "비활성화" || s == "disable" || s == "disabled" || s == "no" || s == "n" || s == "폴스" || s == "거짓" || s == "끄기")
                return false;

            return defaultValue;
        }
    }

    /// <summary>
    /// 설정 파일을 반쯤 쓴 상태로 남기지 않도록 임시 파일에 다 쓴 뒤 바꿔치기한다.
    /// 게임이 저장 도중 꺼지거나 디스크가 가득 차도 원래 config.txt는 그대로 남는다.
    /// </summary>
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string contents, Encoding encoding)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents, encoding);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                }
                catch (IOException)
                {
                    // File.Replace가 안 되는 파일 시스템(일부 네트워크 드라이브 등)은 지우고 옮긴다.
                }

                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
    }
}
