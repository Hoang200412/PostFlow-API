namespace PostFlow.Services.Explainer
{
    /// <summary>Phân bổ số frame cho từng scene sao cho tổng khớp với độ dài audio.</summary>
    public static class SceneTimingAllocator
    {
        public const int MinSceneFrames = 20;

        // Cho phép số từ TTS lệch tối đa ~5% (hoặc 2 từ) so với lời thoại trước khi bỏ qua mốc thời gian thật.
        private const int WordCountToleranceDivisor = 20;
        private const int MinWordCountTolerance = 2;

        /// <param name="sceneWordCounts">Số từ lời thoại của từng scene.</param>
        /// <param name="spokenWordStartsMs">Thời điểm bắt đầu (ms) của từng từ do TTS đọc; có thể rỗng.</param>
        /// <param name="totalFrames">Tổng số frame cần phân bổ.</param>
        /// <param name="fps">Số frame mỗi giây.</param>
        /// <returns>
        /// Số frame của từng scene. Scene cuối nhận phần còn lại, nên tổng bằng totalFrames
        /// (trừ khi phải nâng scene cuối lên mức tối thiểu).
        /// </returns>
        public static IReadOnlyList<int> Allocate(
            IReadOnlyList<int> sceneWordCounts,
            IReadOnlyList<double> spokenWordStartsMs,
            int totalFrames,
            int fps)
        {
            var sceneCount = sceneWordCounts.Count;
            var wordCounts = sceneWordCounts.Select(count => Math.Max(1, count)).ToArray();
            var totalWords = wordCounts.Sum();
            var useSpokenBoundaries = IsAlignedWithNarration(spokenWordStartsMs.Count, totalWords);

            var durations = new int[sceneCount];
            var allocated = 0;
            var wordsBeforeNextScene = 0;

            for (var i = 0; i < sceneCount; i++)
            {
                var isLast = i == sceneCount - 1;
                if (isLast)
                {
                    // Scene cuối nhận toàn bộ frame còn lại để tổng khớp với audio.
                    durations[i] = Math.Max(MinSceneFrames, totalFrames - allocated);
                }
                else
                {
                    wordsBeforeNextScene += wordCounts[i];
                    var desired = useSpokenBoundaries && wordsBeforeNextScene < spokenWordStartsMs.Count
                        ? SecondsToFrames(spokenWordStartsMs[wordsBeforeNextScene] / 1000.0, fps) - allocated
                        : (int)Math.Round((double)wordCounts[i] / totalWords * totalFrames);

                    var remainingScenes = sceneCount - 1 - i;
                    var maxFrames = Math.Max(MinSceneFrames, totalFrames - allocated - remainingScenes * MinSceneFrames);
                    durations[i] = Math.Clamp(desired, MinSceneFrames, maxFrames);
                }

                allocated += durations[i];
            }

            return durations;
        }

        private static bool IsAlignedWithNarration(int spokenWordCount, int narrationWordCount)
        {
            if (spokenWordCount == 0) return false;
            var tolerance = Math.Max(MinWordCountTolerance, narrationWordCount / WordCountToleranceDivisor);
            return Math.Abs(spokenWordCount - narrationWordCount) <= tolerance;
        }

        private static int SecondsToFrames(double seconds, int fps) => (int)Math.Round(seconds * fps);
    }
}
