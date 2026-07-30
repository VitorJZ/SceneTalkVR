using System;
using SceneTalkVR.Core;
using SceneTalkVR.History;

namespace SceneTalkVR.Runtime
{
    internal static class SceneTalkChineseUiText
    {
        public static string TaskName(string taskId, string fallback = "")
        {
            return taskId switch
            {
                "hotel_check_in" => "酒店入住",
                "furniture_shopping" => "家具购买",
                "gym_membership" => "健身房会员咨询",
                "tourist_assistance" => "旅游咨询",
                "pilot_restaurant_walk_in" => "无预约到店",
                "pilot_restaurant_ordering" => "餐厅点餐",
                "pilot_restaurant_wrong_dish" => "餐品错误处理",
                _ => TaskNameFromDisplay(fallback)
            };
        }

        public static string TaskContext(string taskId, string fallback = "")
        {
            return taskId switch
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
        }

        public static string Goal(string goalId, string fallback = "")
        {
            return goalId switch
            {
                "reservation_name" => "提供预订姓名。",
                "breakfast" => "询问是否包含早餐。",
                "higher_floor" => "请求高楼层房间。",
                "checkout_time" => "询问退房时间。",
                "desk_size" => "说明所需书桌尺寸。",
                "material" => "询问可选材质。",
                "budget" => "说明或询问最高预算。",
                "delivery" => "询问是否提供送货上门。",
                "fitness_goal" => "说明健身目标。",
                "monthly_price" => "询问月度会员价格。",
                "suitable_workout" => "询问合适的训练计划。",
                "trial" => "询问是否提供免费体验。",
                "museum_route" => "询问如何前往城市博物馆。",
                "ticket" => "询问是否需要门票。",
                "photography" => "询问室内是否允许拍照。",
                "nearby_attraction" => "询问附近其他景点推荐。",
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
                _ => string.IsNullOrWhiteSpace(fallback) ? "-" : fallback.Trim()
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
                return value;
            }

            if (value.StartsWith("required_item_missing:", StringComparison.Ordinal))
            {
                return "请完成所有必答题。";
            }

            return value switch
            {
                "questionnaire_already_submitted" => "此问卷已经提交。",
                "questionnaire_already_skipped" => "此问卷已经跳过。",
                "questionnaire_not_skippable" => "当前问卷不能跳过。",
                "Avatar voice playback failed. Please retry." => "角色语音播放失败，请点击“重试”。",
                "Correction voice playback failed. Please retry." => "纠错语音播放失败，请点击“重试”。",
                "Speech recognition failed. Please retry recording." => "语音识别失败，请点击“重试”重新录音。",
                "Please try again." => "请重试。",
                "Failed to load default task definition." => "无法加载默认任务。",
                "History operation failed." => "历史记录操作失败。",
                "Experiment history operation failed." => "实验历史记录操作失败。",
                "The selected conversation no longer exists." => "所选对话已不存在。",
                "The selected conversation does not belong to this experiment." => "所选对话不属于当前实验。",
                "The selected questionnaire no longer exists." => "所选问卷已不存在。",
                "formal_ranking_runtime_missing" => "最终排序功能当前不可用。",
                "Correction condition manager is unavailable." => "纠错设置当前不可用。",
                "Open Settings to change the correction condition." => "请打开设置以更改纠错条件。",
                "Available after the current turn." => "当前轮次结束后可更改。",
                "Locked by formal experiment." => "正式实验期间已锁定。",
                "Locked by condition order." => "已由实验条件顺序锁定。",
                _ => "操作失败，请重试。"
            };
        }

        public static string ExperimentKindName(ExperimentKind kind)
        {
            return kind == ExperimentKind.Formal ? "正式实验" : "预实验";
        }

        public static string ExperimentKindName(string value)
        {
            return string.Equals(value, ExperimentKind.Formal.ToString(), StringComparison.OrdinalIgnoreCase)
                ? "正式实验"
                : string.Equals(value, ExperimentKind.Pilot.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? "预实验"
                    : string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        public static string ExperimentStatusName(ExperimentRecordStatus status)
        {
            return status switch
            {
                ExperimentRecordStatus.InProgress => "进行中",
                ExperimentRecordStatus.Completed => "已完成",
                ExperimentRecordStatus.Suspended => "已暂停",
                _ => "未知状态"
            };
        }

        public static string ExperimentAttemptStatusName(ExperimentAttemptStatus status)
        {
            return status switch
            {
                ExperimentAttemptStatus.Running => "进行中",
                ExperimentAttemptStatus.Suspended => "已暂停",
                ExperimentAttemptStatus.Completed => "已完成",
                ExperimentAttemptStatus.TechnicalInvalid => "技术无效",
                ExperimentAttemptStatus.Aborted => "已中止",
                _ => "未知状态"
            };
        }

        public static string QuestionnaireStatusName(QuestionnaireCompletionStatus status)
        {
            return status switch
            {
                QuestionnaireCompletionStatus.InProgress => "填写中",
                QuestionnaireCompletionStatus.Reopened => "已重新打开",
                QuestionnaireCompletionStatus.Submitted => "已提交",
                QuestionnaireCompletionStatus.Skipped => "已跳过",
                QuestionnaireCompletionStatus.Incompatible => "不兼容",
                QuestionnaireCompletionStatus.Rejected => "已拒绝",
                _ => "未开始"
            };
        }

        public static string YesNo(bool value)
        {
            return value ? "是" : "否";
        }

        public static string DisplayValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "unknown" => "未知",
                "none" => "无",
                "male" => "男性",
                "female" => "女性",
                "conservative" => "保守",
                "moderate" => "适中",
                "active" => "积极",
                "slow" => "慢速",
                "normal" => "正常",
                "fast" => "快速",
                "friendly" => "友好",
                "neutral" => "中性",
                "formal" => "正式",
                "grammar" => "语法",
                "unnatural" => "表达不自然",
                "vocabulary" => "词汇",
                "incomplete" => "表达不完整",
                "hotel_lobby" => "酒店大堂",
                "furniture_store" => "家具店",
                "gym" => "健身房",
                "tourist_information_point" => "旅游信息中心",
                "restaurant" => "餐厅",
                "panorama" => "全景场景",
                _ => value.Trim()
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
                return status;
            }

            if (status.StartsWith("Feedback:", StringComparison.OrdinalIgnoreCase))
            {
                return "已生成纠错反馈";
            }

            if (status.StartsWith("Feedback ", StringComparison.OrdinalIgnoreCase))
            {
                return status.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "纠错反馈播放失败"
                    : "纠错反馈播放完成";
            }

            return status switch
            {
                "No correction feedback this turn." => "本轮无需纠错。",
                "Ready for your next line." => "可以继续发言。",
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
