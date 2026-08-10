using System;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class LanguageSystemTests
    {
        private const string PlayerPrefsKey = "SceneTalkVR.UserSettings.v1";
        private bool hadPreviousSettings;
        private string previousSettings;

        [SetUp]
        public void SetUp()
        {
            hadPreviousSettings = PlayerPrefs.HasKey(PlayerPrefsKey);
            previousSettings = hadPreviousSettings ? PlayerPrefs.GetString(PlayerPrefsKey) : string.Empty;
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            ResetSettingsCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (hadPreviousSettings)
            {
                PlayerPrefs.SetString(PlayerPrefsKey, previousSettings);
            }
            else
            {
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
            }

            PlayerPrefs.Save();
            ResetSettingsCache();
        }

        [Test]
        public void LegacySettingsWithoutLanguage_DefaultToChinese()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, "{\"fontScale\":1.0,\"uiScale\":1.0}");
            ResetSettingsCache();

            Assert.That(SceneTalkUserSettingsStore.Current.language, Is.EqualTo(SceneTalkLanguage.Chinese));
        }

        [Test]
        public void InvalidLanguage_NormalizesToChinese()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, "{\"fontScale\":1.0,\"uiScale\":1.0,\"language\":99}");
            ResetSettingsCache();

            Assert.That(SceneTalkUserSettingsStore.Current.language, Is.EqualTo(SceneTalkLanguage.Chinese));
        }

        [Test]
        public void ToggleLanguage_RaisesChangedAndPersistsSelection()
        {
            SceneTalkUserSettingsStore.ResetAll();
            var changedCount = 0;
            Action<SceneTalkUserSettings> handler = _ => changedCount++;
            SceneTalkUserSettingsStore.Changed += handler;
            try
            {
                SceneTalkUserSettingsStore.ToggleLanguage();
            }
            finally
            {
                SceneTalkUserSettingsStore.Changed -= handler;
            }

            Assert.That(SceneTalkUserSettingsStore.Current.language, Is.EqualTo(SceneTalkLanguage.English));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(PlayerPrefsKey), Does.Contain("\"language\":1"));

            ResetSettingsCache();
            Assert.That(SceneTalkUserSettingsStore.Current.language, Is.EqualTo(SceneTalkLanguage.English));
        }

        [Test]
        public void TextResolver_UsesCanonicalTaskQuestionnaireAndTransportTranslations()
        {
            var textType = Type.GetType("SceneTalkVR.Runtime.SceneTalkUiText, Assembly-CSharp");
            Assert.That(textType, Is.Not.Null);

            SceneTalkUserSettingsStore.SetLanguage(SceneTalkLanguage.Chinese);
            Assert.That(Invoke(textType, "TaskName", "hotel_check_in", string.Empty), Is.EqualTo("酒店入住"));
            Assert.That(Invoke(textType, "Goal", "higher_floor", string.Empty), Is.EqualTo("请求高楼层房间。"));

            SceneTalkUserSettingsStore.SetLanguage(SceneTalkLanguage.English);
            Assert.That(Invoke(textType, "Text", "设置"), Is.EqualTo("Settings"));
            Assert.That(Invoke(textType, "TaskName", "hotel_check_in", string.Empty), Is.EqualTo("Hotel Check-In"));
            Assert.That(Invoke(textType, "Goal", "higher_floor", string.Empty), Is.EqualTo("Request a room on a higher floor."));
            Assert.That(Invoke(textType, "Error", "questionnaire_already_skipped"), Is.EqualTo("This questionnaire has already been skipped."));
            Assert.That(
                Invoke(textType, "TransportStatus", GatewayTransportState.UsbReady),
                Is.EqualTo("USB cable"));
            Assert.That(Invoke(textType, "Text", "participant text"), Is.EqualTo("participant text"));
        }

        [Test]
        public void CriticalStaticUiText_HasEnglishEntries()
        {
            var textType = Type.GetType("SceneTalkVR.Runtime.SceneTalkUiText, Assembly-CSharp");
            var hasEnglish = textType?.GetMethod("HasEnglish", BindingFlags.Public | BindingFlags.Static);
            Assert.That(hasEnglish, Is.Not.Null);

            var criticalText = new[]
            {
                "预实验", "正式实验", "实验历史", "对话历史", "导出历史数据", "设置", "退出",
                "显示、纠错与连接", "语言", "字体大小", "界面大小", "对话字幕", "数据通道",
                "上一页", "下一页", "跳过", "提交", "最终排序", "任务目标", "第 1 / 1 页"
            };
            foreach (var value in criticalText)
            {
                Assert.That((bool)hasEnglish.Invoke(null, new object[] { value }), Is.True, value);
            }
        }

        [Test]
        public void EnglishResolvers_CoverEveryCurrentTaskGoalAndStateWithoutChineseFallbacks()
        {
            var textType = Type.GetType("SceneTalkVR.Runtime.SceneTalkUiText, Assembly-CSharp");
            Assert.That(textType, Is.Not.Null);
            SceneTalkUserSettingsStore.SetLanguage(SceneTalkLanguage.English);

            var taskIds = new[]
            {
                "hotel_check_in", "furniture_shopping", "gym_membership", "tourist_assistance",
                "pilot_restaurant_walk_in", "pilot_restaurant_ordering", "pilot_restaurant_wrong_dish"
            };
            foreach (var taskId in taskIds)
            {
                AssertEnglish(Invoke(textType, "TaskName", taskId, "中文任务").ToString(), taskId + " name");
                AssertEnglish(Invoke(textType, "TaskContext", taskId, "中文情境").ToString(), taskId + " context");
            }

            var goalIds = new[]
            {
                "reservation_name", "breakfast", "higher_floor", "checkout_time", "quiet_room", "wifi_access",
                "desk_size", "material", "budget", "delivery", "color_preference", "assembly_service",
                "fitness_goal", "monthly_price", "suitable_workout", "trial", "opening_hours", "student_discount",
                "museum_route", "ticket", "photography", "nearby_attraction", "museum_hours", "visit_duration",
                "no_reservation", "party_size", "window_table_availability", "menu_request", "recommendation",
                "main_course", "dish_price", "dietary_restriction", "drink", "wrong_dish", "extra_charge",
                "replacement_preparation_time"
            };
            foreach (var goalId in goalIds)
            {
                AssertEnglish(Invoke(textType, "Goal", goalId, "中文目标").ToString(), goalId);
            }

            foreach (SceneTalkState state in Enum.GetValues(typeof(SceneTalkState)))
            {
                AssertEnglish(Invoke(textType, "StateName", state).ToString(), state.ToString());
            }

            foreach (GatewayTransportState state in Enum.GetValues(typeof(GatewayTransportState)))
            {
                AssertEnglish(Invoke(textType, "TransportStatus", state).ToString(), state.ToString());
            }

            Assert.That(Invoke(textType, "TaskName", "unknown", "中文任务"), Is.EqualTo("Task"));
            Assert.That(Invoke(textType, "TaskContext", "unknown", "中文情境"), Is.EqualTo("-"));
            Assert.That(Invoke(textType, "Goal", "unknown", "中文目标"), Is.EqualTo("-"));
            Assert.That(Invoke(textType, "DisplayValue", "中文系统值"), Is.EqualTo("-"));
            Assert.That(
                Invoke(textType, "Error", "未知中文系统错误"),
                Is.EqualTo("The operation failed. Please try again."));
        }

        private static object Invoke(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static void AssertEnglish(string value, string context)
        {
            Assert.That(value, Is.Not.Null.And.Not.Empty, context);
            Assert.That(value, Does.Not.Match("[\\u3400-\\u9fff]"), context);
            Assert.That(value, Is.Not.EqualTo("Translation unavailable"), context);
        }

        private static void ResetSettingsCache()
        {
            typeof(SceneTalkUserSettingsStore)
                .GetField("cachedSettings", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
        }
    }
}
