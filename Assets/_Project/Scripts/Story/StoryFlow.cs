using System;
using UnityEngine;

/// <summary>
/// Cổng điều phối cốt truyện (Bước 1): quyết định có phát Story Intro hay không rồi chạy tiếp hành động kế (vào màn chơi).
/// - Mặc định chỉ phát lần đầu cho từng màn (khóa PlayerPrefs "story_seen_{levelId}").
/// - Cài đặt "Luôn xem cốt truyện" (khóa "story_always") bắt phát mọi lần.
/// - Bỏ qua cốt truyện = vẫn tính là đã xem; Thẻ nhiệm vụ của màn luôn hiện ở đầu màn chơi (Bước 2).
/// </summary>
public static class StoryFlow
{
    private const string SeenPrefix = "story_seen_";
    private const string AlwaysKey = "story_always";

    public static bool AlwaysShow
    {
        get { return PlayerPrefs.GetInt(AlwaysKey, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(AlwaysKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private static string SeenKey(int level)
    {
        return level > 0 ? SeenPrefix + level : SeenPrefix + "ending";
    }

    public static bool HasSeen(int level)
    {
        return PlayerPrefs.GetInt(SeenKey(level), 0) == 1;
    }

    public static void MarkSeen(int level)
    {
        PlayerPrefs.SetInt(SeenKey(level), 1);
        PlayerPrefs.Save();
    }

    public static bool ShouldAutoPlay(int level)
    {
        return AlwaysShow || !HasSeen(level);
    }

    public static bool HasStory(int level)
    {
        StoryLevelData d = StoryDatabase.GetLevel(level);
        return d != null && d.pages != null && d.pages.Count > 0;
    }

    /// <summary>
    /// Phát cốt truyện của màn (nếu cần) rồi gọi proceed. force = true: phát dù đã xem (nút "Xem lại cốt truyện").
    /// Không có dữ liệu cốt truyện thì gọi proceed ngay để game luôn vào được màn chơi.
    /// </summary>
    public static void PlayThenContinue(int level, bool force, Action proceed)
    {
        Run(level, StoryDatabase.GetLevel(level), force, proceed);
    }

    /// <summary>Kết truyện (4 trang) sau khi hạ Rồng ở màn 10.</summary>
    public static void PlayEnding(Action proceed)
    {
        Run(0, StoryDatabase.GetEnding(), false, proceed);
    }

    private static void Run(int key, StoryLevelData data, bool force, Action proceed)
    {
        if (StoryIntroScreen.IsOpen) return;   // chặn bấm đúp mở hai lần

        bool hasStory = data != null && data.pages != null && data.pages.Count > 0;
        if (!hasStory || (!force && !ShouldAutoPlay(key)))
        {
            if (proceed != null) proceed();
            return;
        }

        StoryIntroScreen.Show(data, delegate (bool skipped)
        {
            MarkSeen(key);
            if (proceed != null) proceed();
        });
    }
}
