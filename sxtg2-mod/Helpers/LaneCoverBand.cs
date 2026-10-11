using System;

namespace sxtg2.Helpers
{
    /// <summary>
    /// 서든/히든(칼같이 가림)과 페이드 인/아웃(서서히 가림) 설정을 레인 위의 위치 네 개로 합친 것.
    /// 위치는 판정선 = 0%, 노트가 나오는 화면 끝 = 100%.
    ///
    ///   100 ── FarEdge     이 위는 안 보임 (서든 경계 / 페이드 인 시작)
    ///          FarOpaque   여기까지 내려오며 투명 → 불투명 (페이드 인)
    ///          (완전히 보이는 구간)
    ///          NearOpaque  여기부터 불투명 → 투명 (페이드 아웃 시작)
    ///   0 ──── NearEdge    이 아래는 안 보임 (히든 경계 / 페이드 아웃 끝)
    ///
    /// 같은 쪽에 서든과 페이드 인을 같이 켜면 더 많이 가리는 쪽 경계를 쓰고, 완전히 보이기 시작하는 곳은
    /// 페이드 인 설정을 따른다(페이드 인이 서든에 가려진 곳에서 시작하면 서든 경계에서 0으로 시작해 같은 곳에서 불투명).
    /// 히든과 페이드 아웃도 같다.
    /// </summary>
    public readonly struct LaneCoverBand
    {
        public LaneCoverBand(float nearEdge, float nearOpaque, float farOpaque, float farEdge)
        {
            NearEdge = nearEdge;
            NearOpaque = nearOpaque;
            FarOpaque = farOpaque;
            FarEdge = farEdge;
        }

        public float NearEdge { get; }
        public float NearOpaque { get; }
        public float FarOpaque { get; }
        public float FarEdge { get; }

        /// <summary>판정선 쪽에 가림 경계가 있는지(히든이나 페이드 아웃).</summary>
        public bool HasNear => NearOpaque > 0f;

        /// <summary>화면 끝 쪽에 가림 경계가 있는지(서든이나 페이드 인).</summary>
        public bool HasFar => FarOpaque < 100f;

        public bool NearFades => NearOpaque > NearEdge;
        public bool FarFades => FarOpaque < FarEdge;
        public bool Enabled => HasNear || HasFar;

        /// <summary>조금이라도 보이는 구간의 길이(%).</summary>
        public float VisibleSpan => FarEdge - NearEdge;

        public static LaneCoverBand From(
            float sudden, float hidden,
            bool fadeIn, float fadeInStart, float fadeInLength,
            bool fadeOut, float fadeOutStart, float fadeOutLength)
        {
            float farEdge = 100f - sudden;
            float farOpaque = farEdge;
            if (fadeIn)
            {
                farEdge = Math.Min(farEdge, fadeInStart);
                farOpaque = Math.Min(farEdge, Math.Max(0f, fadeInStart - fadeInLength));
            }

            float nearEdge = hidden;
            float nearOpaque = nearEdge;
            if (fadeOut)
            {
                nearEdge = Math.Max(nearEdge, Math.Max(0f, fadeOutStart - fadeOutLength));
                nearOpaque = Math.Max(nearEdge, fadeOutStart);
            }

            return new LaneCoverBand(nearEdge, nearOpaque, farOpaque, farEdge);
        }

        public override string ToString()
        {
            return $"보이는 구간 {NearEdge:0.#}~{FarEdge:0.#}%, 완전히 보이는 구간 {NearOpaque:0.#}~{FarOpaque:0.#}%";
        }
    }
}
