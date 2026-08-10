using System;
using System.Collections.Generic;
using SceneTalkVR.Core;
using SceneTalkVR.History;

namespace SceneTalkVR.Runtime
{
    internal static class SceneTalkUiText
    {
        private static readonly IReadOnlyDictionary<string, string> EnglishByChinese =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["编辑器演示——非参与者数据"] = "Editor demo — non-participant data",
                ["欢迎使用 SceneTalkVR"] = "Welcome to SceneTalkVR",
                ["请稍候，实验人员正在准备下一个任务。"] = "Please wait while the experimenter prepares the next task.",
                ["选择反馈模式"] = "Select feedback mode",
                ["请选择任一可用模式，任务已为你分配。"] = "Select any available mode. Your task has already been assigned.",
                ["实验会话尚未准备"] = "Experiment session is not ready",
                ["实验会话尚未准备。\n请联系实验人员。"] = "The experiment session is not ready.\nPlease contact the experimenter.",
                ["返回"] = "Back",
                ["预实验"] = "Pilot experiment",
                ["正式实验"] = "Formal experiment",
                ["实验历史"] = "Experiment history",
                ["对话历史"] = "Conversation history",
                ["导出历史数据"] = "Export history data",
                ["设置"] = "Settings",
                ["退出"] = "Exit",
                ["暂无对话历史。"] = "No conversation history.",
                ["上一页"] = "Previous",
                ["下一页"] = "Next",
                ["第 1 / 1 页"] = "Page 1 / 1",
                ["向上"] = "Up",
                ["向下"] = "Down",
                ["删除"] = "Delete",
                ["历史详情"] = "History details",
                ["删除历史记录？"] = "Delete history record?",
                ["取消"] = "Cancel",
                ["历史记录错误"] = "History error",
                ["显示、纠错与连接"] = "Display, feedback, and connection",
                ["语言"] = "Language",
                ["中文"] = "Chinese",
                ["字体大小"] = "Font size",
                ["界面大小"] = "Interface size",
                ["对话字幕"] = "Dialogue subtitles",
                ["切换"] = "Switch",
                ["纠错来源"] = "Feedback provider",
                ["辅助角色外观"] = "Assistant appearance",
                ["纠错方式"] = "Feedback style",
                ["数据通道"] = "Data connection",
                ["正在连接"] = "Connecting",
                ["场景与角色需求"] = "Scene and character request",
                ["正在聆听……"] = "Listening…",
                ["识别文本：-"] = "Transcript: -",
                ["开始录音"] = "Start recording",
                ["确认"] = "Confirm",
                ["选择练习任务"] = "Select a practice task",
                ["正在加载场景与角色……"] = "Loading scene and character…",
                ["你：-"] = "You: -",
                ["角色：-"] = "Character: -",
                ["准备就绪"] = "Ready",
                ["发言"] = "Speak",
                ["任务目标"] = "Task goals",
                ["退出实验？"] = "Exit experiment?",
                ["实验尚未完成。系统会保留历史记录，你可以稍后从“实验历史”继续。"] = "The experiment is not complete. Your history will be saved so you can continue later from Experiment History.",
                ["继续实验"] = "Continue experiment",
                ["退出到主页"] = "Exit to home",
                ["继续场景对话"] = "Continue scene conversation",
                ["继续旧对话将保留对话上下文和任务进度；开启新对话将重置本任务进度。"] = "Continuing an earlier conversation preserves its context and task progress. Starting a new conversation resets progress for this task.",
                ["继续所选对话"] = "Continue selected conversation",
                ["开启新对话"] = "Start a new conversation",
                ["暂无实验历史。"] = "No experiment history.",
                ["实验记录"] = "Experiment record",
                ["查看记录"] = "View record",
                ["实验记录详情"] = "Experiment record details",
                ["对话与问卷"] = "Conversations and questionnaires",
                ["对话详情"] = "Conversation details",
                ["问卷详情"] = "Questionnaire details",
                ["删除实验记录？"] = "Delete experiment record?",
                ["实验历史错误"] = "Experiment history error",
                ["可选择"] = "Available",
                ["已完成"] = "Completed",
                ["可重试"] = "Retry available",
                ["进行中"] = "In progress",
                ["继续"] = "Continue",
                ["不可用"] = "Unavailable",
                ["不适用"] = "Not applicable",
                ["已锁定"] = "Locked",
                ["隐藏"] = "Hidden",
                ["显示"] = "Shown",
                ["辅助角色外观会全局保存，并在正式实验开始后锁定。"] = "The assistant appearance is saved globally and locked after the formal experiment starts.",
                ["选择辅助角色后可以更改其外观。"] = "Select the assistant provider to change its appearance.",
                ["正在导出…"] = "Exporting…",
                ["正在检查 USB 数据线和电脑导出服务…"] = "Checking the USB cable and computer export service…",
                ["正在整理实验历史数据…"] = "Preparing experiment history data…",
                ["正在通过 USB 导出到电脑…"] = "Exporting to the computer over USB…",
                ["历史数据已导出到电脑。"] = "History data was exported to the computer.",
                ["历史数据正在导出，请稍候。"] = "History data is being exported. Please wait.",
                ["暂无可导出的实验历史数据。"] = "There is no experiment history to export.",
                ["未检测到电脑导出服务。请连接数据线并启动电脑端后台。"] = "The computer export service was not detected. Connect the USB cable and start the computer backend.",
                ["电脑端导出服务版本不兼容，请重新启动最新后台。"] = "The computer export service is incompatible. Restart the latest backend.",
                ["USB 导出地址配置无效，请联系实验人员。"] = "The USB export endpoint is invalid. Contact the experimenter.",
                ["历史数据过大，电脑端拒绝接收。"] = "The history payload is too large for the computer service.",
                ["电脑无法写入导出目录，请检查磁盘空间和目录权限。"] = "The computer cannot write to the export directory. Check disk space and permissions.",
                ["电脑生成问卷 Excel 文件失败。"] = "The computer failed to generate the questionnaire Excel file.",
                ["电脑上存在冲突的同编号导出，请重新点击导出。"] = "A conflicting export ID exists on the computer. Start the export again.",
                ["PICO 历史数据服务不可用。"] = "The PICO history service is unavailable.",
                ["请检查数据线和电脑端后台。"] = "Check the USB cable and computer backend.",
                ["正在准备下一目标……"] = "Preparing the next goal…",
                ["正在等待本轮语音完整播放……"] = "Waiting for this turn's audio to finish…",
                ["全部目标已完成。"] = "All goals completed.",
                ["所选实验记录已不可用。"] = "The selected experiment record is no longer available.",
                ["没有找到可安全恢复的旧对话，请开启新对话。"] = "No conversation can be safely restored. Start a new conversation.",
                ["请选择要继续的历史对话。"] = "Select a conversation to continue.",
                ["尝试记录"] = "Attempt records",
                ["暂无尝试记录。"] = "No attempt records.",
                ["分区得分"] = "Section scores",
                ["回答记录"] = "Responses",
                ["开场"] = "Opening",
                ["纠错：无"] = "Correction: none",
                ["未保存任何对话轮次。"] = "No conversation turns were saved.",
                ["未知时间"] = "Unknown time",
                ["正在录制你的需求……"] = "Recording your request…",
                ["正在识别语音……"] = "Transcribing speech…",
                ["请检查识别结果，然后确认。"] = "Review the transcript, then confirm.",
                ["点击“录音”开始录制。"] = "Select Record to begin.",
                ["任务失效"] = "Task invalid",
                ["正在加载对话历史……"] = "Loading conversation history…",
                ["正在加载实验历史……"] = "Loading experiment history…",
                ["正在恢复场景、角色与对话上下文……"] = "Restoring the scene, character, and conversation context…",
                ["正在准备角色对话……"] = "Preparing the character conversation…",
                ["正在录音……"] = "Recording…",
                ["正在思考……"] = "Thinking…",
                ["正在播放纠错反馈……"] = "Playing corrective feedback…",
                ["角色正在发言……"] = "The character is speaking…",
                ["正在播放语音……"] = "Playing audio…",
                ["可以继续发言。"] = "You can speak again.",
                ["结束"] = "Finish",
                ["重试"] = "Retry",
                ["录音"] = "Record",
                ["仅限实验查看"] = "Experiment view only",
                ["对话角色"] = "Dialogue character",
                ["辅助角色"] = "Assistant agent",
                ["重述反馈"] = "Recast feedback",
                ["直接纠错"] = "Explicit correction",
                ["仅语音"] = "Voice only",
                ["第三人称角色"] = "Humanoid agent",
                ["悬浮球"] = "Floating orb",
                ["预实验会话设置"] = "Pilot session setup",
                ["参与者编号"] = "Participant ID",
                ["必填"] = "Required",
                ["会话编号"] = "Session ID",
                ["可选——留空则自动生成"] = "Optional — leave blank to generate automatically",
                ["创建预实验会话"] = "Create pilot session",
                ["恢复预实验会话"] = "Resume pilot session",
                ["选择反馈角色外观"] = "Select feedback appearance",
                ["请以任意顺序完成三种外观；已完成的外观不能再次选择。"] = "Complete all three appearances in any order. Completed appearances cannot be selected again.",
                ["餐厅口语任务"] = "Restaurant speaking task",
                ["问卷"] = "Questionnaire",
                ["1 = 非常不同意     7 = 非常同意"] = "1 = Strongly disagree     7 = Strongly agree",
                ["跳过"] = "Skip",
                ["提交"] = "Submit",
                ["最终反馈排序"] = "Final feedback ranking",
                ["请为每种反馈体验指定唯一排名，1 表示最喜欢。"] = "Give each feedback experience a unique rank. Rank 1 is your favorite.",
                ["首选"] = "Preferred",
                ["请说明你更喜欢该反馈体验的原因。"] = "Explain why you prefer this feedback experience.",
                ["提交排序"] = "Submit ranking",
                ["预实验已完成"] = "Pilot experiment completed",
                ["感谢参与，你已完成预实验。"] = "Thank you. You have completed the pilot experiment.",
                ["确认跳过"] = "Confirm skip",
                ["请将每个排名各使用一次。"] = "Use each rank exactly once.",
                ["请选择整体最喜欢的反馈体验。"] = "Select your overall favorite feedback experience.",
                ["请说明原因。"] = "Please provide a reason.",
                ["仅语音反馈"] = "Voice-only feedback",
                ["悬浮球反馈"] = "Floating-orb feedback",
                ["人形辅助角色反馈"] = "Humanoid-assistant feedback",
                ["最终排序"] = "Final ranking",
                ["请为每种反馈模式指定唯一排名，1 表示最喜欢，4 表示最不喜欢。"] = "Give each feedback mode a unique rank. Rank 1 is most preferred and rank 4 is least preferred.",
                ["请说明你最喜欢该模式的原因。"] = "Explain why you prefer this mode.",
                ["实验已完成"] = "Experiment completed",
                ["感谢参与，你已完成正式实验。"] = "Thank you. You have completed the formal experiment.",
                ["此排序已经提交。"] = "This ranking has already been submitted.",
                ["请将 1 至 4 的每个排名各使用一次。"] = "Use each rank from 1 through 4 exactly once.",
                ["请选择整体最喜欢的反馈模式。"] = "Select your overall favorite feedback mode.",
                ["请简要说明排序原因。"] = "Briefly explain your ranking.",
                ["对话角色——直接纠错（NE）"] = "Dialogue character — explicit correction (NE)",
                ["对话角色——重述反馈（NR）"] = "Dialogue character — recast feedback (NR)",
                ["辅助角色——直接纠错（SE）"] = "Assistant agent — explicit correction (SE)",
                ["辅助角色——重述反馈（SR）"] = "Assistant agent — recast feedback (SR)",
                ["确认提交"] = "Confirm submit",
                ["请再次点击以确认提交。"] = "Select Confirm Submit to submit the questionnaire.",
                ["已提交"] = "Submitted",
                ["跳过将保留已填写内容并继续实验，请再次点击“确认跳过”。"] = "Skipping keeps completed answers and continues the experiment. Select Confirm Skip to continue.",
                ["已跳过"] = "Skipped",
                ["请完成所有必答题。"] = "Complete all required questions.",
                ["所有必答题均已完成。"] = "All required questions are complete.",
                ["PICO 设备验证已完成。\n此数据不是参与者数据，不具备采集资格。"] = "PICO device validation completed.\nThis is not participant data and is not eligible for collection."
            };

        public static SceneTalkLanguage Language => SceneTalkUserSettingsStore.Current.language;
        public static bool IsEnglish => Language == SceneTalkLanguage.English;

        public static string Select(string chinese, string english)
        {
            return IsEnglish ? english ?? string.Empty : chinese ?? string.Empty;
        }

        public static string Text(string chinese)
        {
            if (!IsEnglish || string.IsNullOrEmpty(chinese))
            {
                return chinese ?? string.Empty;
            }

            if (EnglishByChinese.TryGetValue(chinese, out var english))
            {
                return english;
            }

            return ContainsChinese(chinese) ? "Translation unavailable" : chinese;
        }

        public static bool HasEnglish(string chinese)
        {
            return string.IsNullOrEmpty(chinese)
                || !ContainsChinese(chinese)
                || EnglishByChinese.ContainsKey(chinese);
        }

        public static string TaskName(string taskId, string fallback = "")
        {
            var chinese = taskId switch
            {
                "hotel_check_in" => "酒店入住",
                "furniture_shopping" => "家具购物",
                "gym_membership" => "健身房会员",
                "tourist_assistance" => "游客咨询",
                "pilot_restaurant_walk_in" => "无预约到店",
                "pilot_restaurant_ordering" => "餐厅点餐",
                "pilot_restaurant_wrong_dish" => "餐品错误处理",
                _ => TaskNameFromDisplay(fallback)
            };

            if (!IsEnglish)
            {
                return chinese;
            }

            return taskId switch
            {
                "hotel_check_in" => "Hotel Check-In",
                "furniture_shopping" => "Furniture Shopping",
                "gym_membership" => "Gym Membership",
                "tourist_assistance" => "Tourist Assistance",
                "pilot_restaurant_walk_in" => "Walk-in Without Reservation",
                "pilot_restaurant_ordering" => "Ordering a Meal",
                "pilot_restaurant_wrong_dish" => "Wrong Dish Problem",
                _ => string.IsNullOrWhiteSpace(fallback) || ContainsChinese(fallback)
                    ? "Task"
                    : fallback.Trim()
            };
        }

        public static string TaskContext(string taskId, string fallback = "")
        {
            var chinese = taskId switch
            {
                "hotel_check_in" => "在酒店办理入住并确认住宿信息。",
                "furniture_shopping" => "与家具销售人员沟通并购买书桌。",
                "gym_membership" => "向健身房工作人员咨询会员与训练计划。",
                "tourist_assistance" => "向旅游信息中心工作人员咨询城市游览信息。",
                "pilot_restaurant_walk_in" => "未预约到达餐厅，并询问是否有空桌。",
                "pilot_restaurant_ordering" => "查看餐厅菜单并完成点餐。",
                "pilot_restaurant_wrong_dish" => "服务员上错餐品后，礼貌地解决问题。",
                _ => GoalFromDisplay(fallback)
            };

            if (!IsEnglish)
            {
                return chinese;
            }

            if (!string.IsNullOrWhiteSpace(fallback) && !ContainsChinese(fallback))
            {
                return fallback.Trim();
            }

            return taskId switch
            {
                "hotel_check_in" => "Checking in at a hotel and confirming accommodation details.",
                "furniture_shopping" => "Speaking with a furniture salesperson to purchase a desk.",
                "gym_membership" => "Asking at a gym about membership and a training plan.",
                "tourist_assistance" => "Asking staff at a tourist information point for city-visit information.",
                "pilot_restaurant_walk_in" => "Arriving at a restaurant without a reservation and asking whether a table is available.",
                "pilot_restaurant_ordering" => "Looking at a restaurant menu and ordering a meal.",
                "pilot_restaurant_wrong_dish" => "Politely resolving a problem after the restaurant server brings the wrong dish.",
                _ => string.IsNullOrWhiteSpace(fallback) || ContainsChinese(fallback)
                    ? "-"
                    : fallback.Trim()
            };
        }

        public static string Goal(string goalId, string fallback = "")
        {
            var chinese = goalId switch
            {
                "reservation_name" => "提供预订人姓名。",
                "breakfast" => "询问是否包含早餐。",
                "higher_floor" => "请求高楼层房间。",
                "checkout_time" => "询问退房时间。",
                "quiet_room" => "请求远离噪声区域的安静房间。",
                "wifi_access" => "询问 Wi-Fi 或互联网接入。",
                "desk_size" => "说明需要的书桌尺寸。",
                "material" => "询问可选材质。",
                "budget" => "说明或询问最高预算。",
                "delivery" => "询问是否提供送货上门。",
                "color_preference" => "询问书桌可选颜色。",
                "assembly_service" => "询问是否提供组装服务。",
                "fitness_goal" => "说明健身目标。",
                "monthly_price" => "询问月度会员价格。",
                "suitable_workout" => "询问合适的训练计划。",
                "trial" => "询问是否提供免费体验。",
                "opening_hours" => "询问健身房营业时间。",
                "student_discount" => "询问是否提供学生优惠。",
                "museum_route" => "询问如何前往城市博物馆。",
                "ticket" => "询问是否需要门票。",
                "photography" => "询问室内是否允许拍照。",
                "nearby_attraction" => "询问附近其他景点推荐。",
                "museum_hours" => "询问博物馆开放时间。",
                "visit_duration" => "询问参观博物馆通常需要多长时间。",
                "no_reservation" => "说明自己没有预约。",
                "party_size" => "确定用餐人数。",
                "table_availability" => "询问是否有空桌。",
                "wait_time" => "询问需要等待多久。",
                "window_table_availability" => "询问是否有靠窗的空桌。",
                "menu_request" => "要一份菜单。",
                "recommendation" => "询问推荐菜品。",
                "main_course" => "点一份主菜。",
                "dish_price" => "询问菜品价格。",
                "dietary_restriction" => "声明自己的忌口或过敏原。",
                "drink" => "点一份饮品。",
                "wrong_dish" => "说明收到的餐品不正确。",
                "original_order" => "说明原本点的餐品。",
                "replacement_request" => "请求更换餐品。",
                "replacement_wait_time" => "询问更换餐品需要多久。",
                "extra_charge" => "询问是否有额外收费。",
                "replacement_preparation_time" => "询问重新制作餐品所需时间。",
                _ => string.IsNullOrWhiteSpace(fallback) || ContainsChinese(fallback)
                    ? "-"
                    : fallback.Trim()
            };

            if (!IsEnglish)
            {
                return chinese;
            }

            if (!string.IsNullOrWhiteSpace(fallback) && !ContainsChinese(fallback))
            {
                return fallback.Trim();
            }

            return goalId switch
            {
                "reservation_name" => "Provide the reservation name.",
                "breakfast" => "Ask whether breakfast is included.",
                "higher_floor" => "Request a room on a higher floor.",
                "checkout_time" => "Ask about the check-out time.",
                "quiet_room" => "Request a quiet room away from noisy areas.",
                "wifi_access" => "Ask about Wi-Fi or internet access.",
                "desk_size" => "Describe the desk size needed.",
                "material" => "Ask about available materials.",
                "budget" => "State or ask about the maximum budget.",
                "delivery" => "Ask whether home delivery is available.",
                "color_preference" => "Ask about the available desk colors.",
                "assembly_service" => "Ask whether assembly service is available.",
                "fitness_goal" => "Explain a fitness goal.",
                "monthly_price" => "Ask about the monthly membership price.",
                "suitable_workout" => "Ask about a suitable workout plan.",
                "trial" => "Ask whether a free trial is available.",
                "opening_hours" => "Ask about the gym's opening hours.",
                "student_discount" => "Ask whether a student discount is available.",
                "museum_route" => "Ask how to reach the city museum.",
                "ticket" => "Ask whether a ticket is required.",
                "photography" => "Ask whether indoor photography is allowed.",
                "nearby_attraction" => "Ask for another nearby attraction recommendation.",
                "museum_hours" => "Ask about the museum's opening hours.",
                "visit_duration" => "Ask how long a typical museum visit takes.",
                "no_reservation" => "Explain that you do not have a reservation.",
                "party_size" => "State the number of diners.",
                "table_availability" => "Ask whether a table is available.",
                "wait_time" => "Ask how long the wait will be.",
                "window_table_availability" => "Ask whether a window table is available.",
                "menu_request" => "Ask for a menu.",
                "recommendation" => "Ask for a recommended dish.",
                "main_course" => "Order one main course.",
                "dish_price" => "Ask about the price of a dish.",
                "dietary_restriction" => "State a dietary restriction or allergen.",
                "drink" => "Order a drink.",
                "wrong_dish" => "Explain that the received dish is incorrect.",
                "original_order" => "State what was originally ordered.",
                "replacement_request" => "Ask for the dish to be replaced.",
                "replacement_wait_time" => "Ask how long the replacement will take.",
                "extra_charge" => "Ask whether there will be an extra charge.",
                "replacement_preparation_time" => "Ask how long the replacement dish will take to prepare.",
                _ => string.IsNullOrWhiteSpace(fallback) || ContainsChinese(fallback)
                    ? "-"
                    : fallback.Trim()
            };
        }

        public static string Error(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return string.Empty;
            }

            var value = error.Trim();
            if (ContainsChinese(value))
            {
                if (!IsEnglish)
                {
                    return value;
                }

                return EnglishByChinese.TryGetValue(value, out var translated)
                    ? translated
                    : "The operation failed. Please try again.";
            }

            if (value.StartsWith("required_item_missing:", StringComparison.Ordinal))
            {
                return Select("请完成所有必答题。", "Complete all required questions.");
            }

            return value switch
            {
                "questionnaire_already_submitted" => Select("此问卷已经提交。", "This questionnaire has already been submitted."),
                "questionnaire_already_skipped" => Select("此问卷已经跳过。", "This questionnaire has already been skipped."),
                "questionnaire_not_skippable" => Select("当前问卷不能跳过。", "This questionnaire cannot be skipped."),
                "Avatar voice playback failed. Please retry." => Select("角色语音播放失败，请点击“重试”。", "Character audio playback failed. Select Retry."),
                "Correction voice playback failed. Please retry." => Select("纠错语音播放失败，请点击“重试”。", "Corrective-feedback audio playback failed. Select Retry."),
                "Speech recognition failed. Please retry recording." => Select("语音识别失败，请点击“重试”重新录音。", "Speech recognition failed. Select Retry to record again."),
                "This task attempt is no longer valid. Please retry the task." => Select("本轮任务因技术问题失效，请退出并重新进入该任务。", "This task attempt is invalid because of a technical problem. Exit and reopen the task."),
                "Please try again." => Select("请重试。", "Please try again."),
                "Failed to load default task definition." => Select("无法加载默认任务。", "The default task could not be loaded."),
                "History operation failed." => Select("历史记录操作失败。", "The history operation failed."),
                "Experiment history operation failed." => Select("实验历史记录操作失败。", "The experiment history operation failed."),
                "The selected conversation no longer exists." => Select("所选对话已不存在。", "The selected conversation no longer exists."),
                "The selected conversation does not belong to this experiment." => Select("所选对话不属于当前实验。", "The selected conversation does not belong to this experiment."),
                "The selected questionnaire no longer exists." => Select("所选问卷已不存在。", "The selected questionnaire no longer exists."),
                "formal_ranking_runtime_missing" => Select("最终排序功能当前不可用。", "Final ranking is currently unavailable."),
                "Correction condition manager is unavailable." => Select("纠错设置当前不可用。", "Feedback settings are currently unavailable."),
                "Open Settings to change the correction condition." => Select("请打开设置以更改纠错条件。", "Open Settings to change the feedback condition."),
                "Available after the current turn." => Select("当前轮次结束后可更改。", "Available after the current turn."),
                "Locked by formal experiment." => Select("正式实验期间已锁定。", "Locked during the formal experiment."),
                "Locked by condition order." => Select("已由实验条件顺序锁定。", "Locked by the experiment condition order."),
                _ => Select("操作失败，请重试。", "The operation failed. Please try again.")
            };
        }

        public static string ExperimentKindName(ExperimentKind kind)
        {
            return kind == ExperimentKind.Formal
                ? Select("正式实验", "Formal experiment")
                : Select("预实验", "Pilot experiment");
        }

        public static string ExperimentKindName(string value)
        {
            return string.Equals(value, ExperimentKind.Formal.ToString(), StringComparison.OrdinalIgnoreCase)
                ? Select("正式实验", "Formal experiment")
                : string.Equals(value, ExperimentKind.Pilot.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? Select("预实验", "Pilot experiment")
                    : string.IsNullOrWhiteSpace(value) || (IsEnglish && ContainsChinese(value))
                        ? "-"
                        : value.Trim();
        }

        public static string ExperimentStatusName(ExperimentRecordStatus status)
        {
            return status switch
            {
                ExperimentRecordStatus.InProgress => Select("进行中", "In progress"),
                ExperimentRecordStatus.Completed => Select("已完成", "Completed"),
                ExperimentRecordStatus.Suspended => Select("已暂停", "Suspended"),
                _ => Select("未知状态", "Unknown status")
            };
        }

        public static string ExperimentAttemptStatusName(ExperimentAttemptStatus status)
        {
            return status switch
            {
                ExperimentAttemptStatus.Running => Select("进行中", "In progress"),
                ExperimentAttemptStatus.Suspended => Select("已暂停", "Suspended"),
                ExperimentAttemptStatus.Completed => Select("已完成", "Completed"),
                ExperimentAttemptStatus.TechnicalInvalid => Select("技术无效", "Technically invalid"),
                ExperimentAttemptStatus.Aborted => Select("已中止", "Aborted"),
                _ => Select("未知状态", "Unknown status")
            };
        }

        public static string QuestionnaireStatusName(QuestionnaireCompletionStatus status)
        {
            return status switch
            {
                QuestionnaireCompletionStatus.InProgress => Select("填写中", "In progress"),
                QuestionnaireCompletionStatus.Reopened => Select("已重新打开", "Reopened"),
                QuestionnaireCompletionStatus.Submitted => Select("已提交", "Submitted"),
                QuestionnaireCompletionStatus.Skipped => Select("已跳过", "Skipped"),
                QuestionnaireCompletionStatus.Incompatible => Select("不兼容", "Incompatible"),
                QuestionnaireCompletionStatus.Rejected => Select("已拒绝", "Rejected"),
                _ => Select("未开始", "Not started")
            };
        }

        public static string YesNo(bool value)
        {
            return value ? Select("是", "Yes") : Select("否", "No");
        }

        public static string StateName(SceneTalkState state)
        {
            if (IsEnglish)
            {
                return state switch
                {
                    SceneTalkState.Idle => "Idle",
                    SceneTalkState.Settings => "Settings",
                    SceneTalkState.ExperimentSelection => "Experiment selection",
                    SceneTalkState.ExperimentPhase => "Experiment phase",
                    SceneTalkState.ExperimentRanking => "Experiment ranking",
                    SceneTalkState.ExperimentCompleted => "Experiment completed",
                    SceneTalkState.ExperimentExitConfirm => "Confirm experiment exit",
                    SceneTalkState.ExperimentHistoryLoading => "Loading experiment history",
                    SceneTalkState.ExperimentHistoryList => "Experiment history",
                    SceneTalkState.ExperimentHistoryActions => "Experiment record actions",
                    SceneTalkState.ExperimentHistoryRecord => "Experiment record",
                    SceneTalkState.ExperimentHistoryConversationDetail => "Experiment conversation details",
                    SceneTalkState.ExperimentHistoryQuestionnaireDetail => "Experiment questionnaire details",
                    SceneTalkState.ExperimentHistoryDeleteConfirm => "Confirm experiment record deletion",
                    SceneTalkState.ExperimentHistoryError => "Experiment history error",
                    SceneTalkState.HistoryLoading => "Loading conversation history",
                    SceneTalkState.HistoryList => "Conversation history",
                    SceneTalkState.HistoryDetail => "Conversation details",
                    SceneTalkState.HistoryDeleteConfirm => "Confirm conversation deletion",
                    SceneTalkState.HistoryRestoring => "Restoring conversation",
                    SceneTalkState.HistoryError => "Conversation history error",
                    SceneTalkState.Listening => "Listening",
                    SceneTalkState.Recording => "Recording",
                    SceneTalkState.Transcribing => "Transcribing speech",
                    SceneTalkState.Processing => "Processing",
                    SceneTalkState.SceneReady => "Scene ready",
                    SceneTalkState.AvatarSpeaking => "Character speaking",
                    SceneTalkState.CorrectionFeedbackSpeaking => "Playing corrective feedback",
                    SceneTalkState.DialogueSpeaking => "Playing dialogue audio",
                    SceneTalkState.TurnReview => "Turn review",
                    SceneTalkState.Questionnaire => "Questionnaire",
                    SceneTalkState.Finished => "Completed",
                    SceneTalkState.Error => "Error",
                    SceneTalkState.ExperimentConversationResumeChoice => "Conversation resume choice",
                    _ => "Unknown state"
                };
            }

            return state switch
            {
                SceneTalkState.Idle => "空闲",
                SceneTalkState.Settings => "设置",
                SceneTalkState.ExperimentSelection => "实验选择",
                SceneTalkState.ExperimentPhase => "实验阶段",
                SceneTalkState.ExperimentRanking => "实验排序",
                SceneTalkState.ExperimentCompleted => "实验完成",
                SceneTalkState.ExperimentExitConfirm => "退出实验确认",
                SceneTalkState.ExperimentHistoryLoading => "正在加载实验历史",
                SceneTalkState.ExperimentHistoryList => "实验历史列表",
                SceneTalkState.ExperimentHistoryActions => "实验记录操作",
                SceneTalkState.ExperimentHistoryRecord => "实验记录",
                SceneTalkState.ExperimentHistoryConversationDetail => "实验对话详情",
                SceneTalkState.ExperimentHistoryQuestionnaireDetail => "实验问卷详情",
                SceneTalkState.ExperimentHistoryDeleteConfirm => "删除实验记录确认",
                SceneTalkState.ExperimentHistoryError => "实验历史错误",
                SceneTalkState.HistoryLoading => "正在加载对话历史",
                SceneTalkState.HistoryList => "对话历史列表",
                SceneTalkState.HistoryDetail => "对话历史详情",
                SceneTalkState.HistoryDeleteConfirm => "删除对话历史确认",
                SceneTalkState.HistoryRestoring => "正在恢复对话",
                SceneTalkState.HistoryError => "对话历史错误",
                SceneTalkState.Listening => "正在聆听",
                SceneTalkState.Recording => "正在录音",
                SceneTalkState.Transcribing => "正在识别语音",
                SceneTalkState.Processing => "正在处理",
                SceneTalkState.SceneReady => "场景就绪",
                SceneTalkState.AvatarSpeaking => "角色正在发言",
                SceneTalkState.CorrectionFeedbackSpeaking => "正在播放纠错反馈",
                SceneTalkState.DialogueSpeaking => "正在播放对话语音",
                SceneTalkState.TurnReview => "轮次确认",
                SceneTalkState.Questionnaire => "问卷",
                SceneTalkState.Finished => "已完成",
                SceneTalkState.Error => "错误",
                SceneTalkState.ExperimentConversationResumeChoice => "继续对话选择",
                _ => "未知状态"
            };
        }

        public static string TransportStatus(GatewayTransportState state)
        {
            return state switch
            {
                GatewayTransportState.UsbReady => Select("USB 数据线", "USB cable"),
                GatewayTransportState.LanReady => Select("局域网备用", "LAN fallback"),
                GatewayTransportState.Unavailable => Select("不可用", "Unavailable"),
                _ => Select("正在连接", "Connecting")
            };
        }

        public static string DisplayValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "unknown" => Select("未知", "Unknown"),
                "none" => Select("无", "None"),
                "male" => Select("男性", "Male"),
                "female" => Select("女性", "Female"),
                "conservative" => Select("保守", "Conservative"),
                "moderate" => Select("适中", "Moderate"),
                "active" => Select("积极", "Active"),
                "slow" => Select("慢速", "Slow"),
                "normal" => Select("正常", "Normal"),
                "fast" => Select("快速", "Fast"),
                "friendly" => Select("友好", "Friendly"),
                "neutral" => Select("中性", "Neutral"),
                "formal" => Select("正式", "Formal"),
                "grammar" => Select("语法", "Grammar"),
                "unnatural" => Select("表达不自然", "Unnatural phrasing"),
                "vocabulary" => Select("词汇", "Vocabulary"),
                "incomplete" => Select("表达不完整", "Incomplete expression"),
                "hotel_lobby" => Select("酒店大堂", "Hotel lobby"),
                "furniture_store" => Select("家具店", "Furniture store"),
                "gym" => Select("健身房", "Gym"),
                "tourist_information_point" => Select("旅游信息中心", "Tourist information point"),
                "restaurant" => Select("餐厅", "Restaurant"),
                "panorama" => Select("全景场景", "Panoramic scene"),
                _ => IsEnglish && ContainsChinese(value) ? "-" : value.Trim()
            };
        }

        public static string CorrectionStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var status = value.Trim();
            if (ContainsChinese(status))
            {
                return Text(status);
            }

            if (status.StartsWith("Feedback:", StringComparison.OrdinalIgnoreCase))
            {
                return Select("已生成纠错反馈", "Corrective feedback generated");
            }

            if (status.StartsWith("Feedback ", StringComparison.OrdinalIgnoreCase))
            {
                return status.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Select("纠错反馈播放失败", "Corrective-feedback playback failed")
                    : Select("纠错反馈播放完成", "Corrective-feedback playback completed");
            }

            return status switch
            {
                "No correction feedback this turn." => Select("本轮无需纠错。", "No corrective feedback this turn."),
                "Ready for your next line." => Select("可以继续发言。", "You can speak again."),
                _ => Error(status)
            };
        }

        private static string TaskNameFromDisplay(string fallback)
        {
            return fallback switch
            {
                "Hotel Check-In" => "酒店入住",
                "Furniture Shopping" => "家具购买",
                "Gym Membership" => "健身房会员咨询",
                "Tourist Assistance" => "旅游咨询",
                "Walk-in Without Reservation" => "无预约到店",
                "Ordering a Meal" => "餐厅点餐",
                "Wrong Dish Problem" => "餐品错误处理",
                _ => string.IsNullOrWhiteSpace(fallback) ? "任务" : fallback.Trim()
            };
        }

        private static string GoalFromDisplay(string fallback)
        {
            return fallback switch
            {
                "Provide the reservation name." => "提供预订姓名。",
                "Ask whether breakfast is included." => "询问是否包含早餐。",
                "Request a room on a higher floor." => "请求高楼层房间。",
                "Ask about the check-out time." => "询问退房时间。",
                "Describe the desk size needed." => "说明所需书桌尺寸。",
                "Ask about available materials." => "询问可选材质。",
                "State or ask about the maximum budget." => "说明或询问最高预算。",
                "Ask whether home delivery is available." => "询问是否提供送货上门。",
                "Explain a fitness goal." => "说明健身目标。",
                "Ask about the monthly membership price." => "询问月度会员价格。",
                "Ask about a suitable workout plan." => "询问合适的训练计划。",
                "Ask whether a free trial is available." => "询问是否提供免费体验。",
                "Ask how to reach the city museum." => "询问如何前往城市博物馆。",
                "Ask whether a ticket is required." => "询问是否需要门票。",
                "Ask whether indoor photography is allowed." => "询问室内是否允许拍照。",
                "Ask for another nearby attraction recommendation." => "询问附近其他景点推荐。",
                "Explain that you do not have a reservation." => "说明自己没有预约。",
                "State the number of diners." => "确定用餐人数。",
                "Ask whether a table is available." => "询问是否有空桌。",
                "Ask how long the wait will be." => "询问需要等待多久。",
                "Ask whether a window table is available." => "询问是否有靠窗的空桌。",
                "Ask for a menu." => "要一份菜单。",
                "Ask for a recommended dish." => "询问推荐菜品。",
                "Order one main course." => "点一份主菜。",
                "Explain a dietary requirement or food restriction." => "说明饮食要求或忌口。",
                "Ask about the price of a dish." => "询问菜品价格。",
                "State a dietary restriction or allergen." => "声明自己的忌口或过敏原。",
                "Order a drink." => "点一份饮品。",
                "Explain that the received dish is incorrect." => "说明收到的餐品不正确。",
                "State what was originally ordered." => "说明原本点的餐品。",
                "Ask for the dish to be replaced." => "请求更换餐品。",
                "Ask how long the replacement will take." => "询问更换餐品需要多久。",
                "Ask whether there will be an extra charge." => "询问是否有额外收费。",
                "Ask how long the replacement dish will take to prepare." => "询问重新制作餐品所需时间。",
                _ => string.IsNullOrWhiteSpace(fallback) ? "-" : fallback.Trim()
            };
        }

        private static bool ContainsChinese(string value)
        {
            foreach (var character in value)
            {
                if (character >= '\u3400' && character <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
