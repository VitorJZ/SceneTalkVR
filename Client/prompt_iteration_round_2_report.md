# 🧪 SceneTalk VR: 纠错提示词快速迭代测试报告 (第 2 轮)

本报告包含 5 种典型语法偏误用例以及 3 种正确无误输入的测试反馈。通过对提示词的升级（引入显式 recast 单词禁令和显性教学感扩展），我们对生成表现进行了验证。

## 测试用例 1: Subject-Verb Agreement (He go -> He goes) (语法错误输入)
*   **场景**: `restaurant_reservation` (角色: `barista`)
*   **用户输入**: *"He go to restaurant tomorrow."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Great! So you're planning to come in tomorrow. How many people will be in your party?" | "You can say: 'He goes to the restaurant tomorrow.'" | — |
| `dialogue_avatar_recast` | `True` | "Oh, you're planning to come in tomorrow? How many people will be joining you?" | "Oh, you're going to the restaurant tomorrow?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great, so you're planning to come in tomorrow. How many people will be in your party?" | "Grammar tip: Use 'goes' for third-person singular subjects like 'he', and add 'the' before 'restaurant'. Say: 'He goes to the restaurant tomorrow.'" | — |
| `assistant_agent_recast` | `True` | "Great! So you're planning to come in tomorrow. What time would you like to arrive, and how many people will be in your party?" | "He goes to the restaurant tomorrow." | 🟢 PASS |

---

## 测试用例 2: Adverb Modifier (I very like -> I really like) (语法错误输入)
*   **场景**: `furniture_shopping` (角色: `clerk`)
*   **用户输入**: *"I very like this furniture."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "That's great to hear! What kind of furniture caught your eye?" | "You can say: "I really like this furniture."" | — |
| `dialogue_avatar_recast` | `True` | "Oh, you really like this furniture? That's great to hear! What is it about this piece that catches your eye?" | "Oh, you really like this furniture?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "That's great to hear! What kind of furniture caught your eye?" | "Grammar tip: Use 'really' before a verb, not 'very.' Say: 'I really like this furniture.'" | — |
| `assistant_agent_recast` | `True` | "That's great to hear! Is there anything specific you're looking for, like a desk or a chair?" | "I really like this furniture." | 🟢 PASS |

---

## 测试用例 3: Question Structure (How much cost -> How much does it cost) (语法错误输入)
*   **场景**: `gym_membership` (角色: `instructor`)
*   **用户输入**: *"How much cost the gym plan?"*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Great question! Our monthly plan is $50, and the yearly plan is $480, which saves you two months." | "You can say: 'How much does the gym plan cost?'" | — |
| `dialogue_avatar_recast` | `True` | "Great question! Our monthly plan costs $50, and the yearly plan is $500." | "Oh, you're asking how much the gym plan costs?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great question! Our monthly plan is $50, and the yearly plan is $500. Which one sounds better to you?" | "Grammar tip: Use 'does' before the subject in questions. Say: 'How much does the gym plan cost?'" | — |
| `assistant_agent_recast` | `True` | "Great question! Our monthly plan is $49, and the yearly plan is $499, which saves you about two months." | "How much does the gym plan cost?" | 🟢 PASS |

---

## 测试用例 4: Missing Article (I have reservation -> I have a reservation) (语法错误输入)
*   **场景**: `hotel_check_in` (角色: `clerk`)
*   **用户输入**: *"I have reservation under Johnson."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Great, let me look that up. I see a reservation for Johnson. Could you confirm the spelling of your first name?" | "You can say: 'I have a reservation under Johnson.'" | — |
| `dialogue_avatar_recast` | `True` | "Great, let me look that up. I see a reservation under Johnson for a standard room. Is that correct?" | "Great, let me look that up. I see a reservation under Johnson for a standard room." | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great, let me look that up. Welcome, Mr. Johnson! I have you down for a standard room for two nights. Is that correct?" | "Grammar tip: Use the article 'a' before 'reservation.' Say: 'I have a reservation under Johnson.'" | — |
| `assistant_agent_recast` | `True` | "Great, let me look that up. Welcome, Mr. Johnson! I have you down for a deluxe room for two nights. Is that correct?" | "I have a reservation under Johnson." | 🟢 PASS |

---

## 测试用例 5: Verb Collocation (make muscle -> build muscle / get in shape) (语法错误输入)
*   **场景**: `gym_membership` (角色: `instructor`)
*   **用户输入**: *"I want make muscle in gym."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "That's great! We have a full weight room and personal trainers to help you build muscle. Are you thinking about a monthly or yearly plan?" | "You can say: 'I want to build muscle at the gym.'" | — |
| `dialogue_avatar_recast` | `True` | "That's great! We have a full weight room and personal trainers to help you build muscle. Are you interested in a monthly or yearly plan?" | "Oh, you want to build muscle in the gym?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "That's great! We have a lot of equipment to help you build muscle. Are you interested in a monthly plan, a yearly plan, or a trial visit?" | "Grammar tip: Use 'to' after 'want' and 'the' before 'gym'. Say: 'I want to build muscle in the gym.'" | — |
| `assistant_agent_recast` | `True` | "That's great! We have a full weight room and personal trainers who can help you build muscle. Are you interested in a monthly or yearly plan?" | "I want to build muscle at the gym." | 🟢 PASS |

---

## 测试用例 6: False Positive Check: Table for two, please. (正确无误输入 (测试 False Positive))
*   **场景**: `restaurant_reservation` (角色: `barista`)
*   **用户输入**: *"Table for two, please."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `False` | "Sure, a table for two. What date and time are you looking for?" | "" | — |
| `dialogue_avatar_recast` | `False` | "Sure, a table for two. What date and time are you looking for?" | "" | 🟢 PASS |
| `assistant_agent_explicit` | `False` | "Sure, a table for two. What date and time are you looking for?" | "" | — |
| `assistant_agent_recast` | `False` | "Sure, a table for two. What date and time are you looking for?" | "" | 🟢 PASS |

---

## 测试用例 7: False Positive Check: I have a reservation under Johnson. (正确无误输入 (测试 False Positive))
*   **场景**: `hotel_check_in` (角色: `clerk`)
*   **用户输入**: *"I have a reservation under Johnson."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `False` | "Great, let me look that up. Welcome, Mr. Johnson! I have you down for a deluxe room for two nights. Is that correct?" | "" | — |
| `dialogue_avatar_recast` | `False` | "Great, let me look that up. Welcome, Mr. Johnson! I have you down for a deluxe room for two nights. Is that correct?" | "" | 🟢 PASS |
| `assistant_agent_explicit` | `False` | "Great, let me look that up. Yes, I see your reservation here, Mr. Johnson. You're all set for a standard room for two nights." | "" | — |
| `assistant_agent_recast` | `False` | "Great, let me look that up. Welcome, Mr. Johnson! I have you down for a deluxe room for two nights. Is that correct?" | "" | 🟢 PASS |

---

## 测试用例 8: False Positive Check: Could you tell me how much the monthly plan costs? (正确无误输入 (测试 False Positive))
*   **场景**: `gym_membership` (角色: `instructor`)
*   **用户输入**: *"Could you tell me how much the monthly plan costs?"*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `False` | "Sure! The monthly plan is $49.99 per month, and it gives you full access to all our gym facilities and group classes." | "" | — |
| `dialogue_avatar_recast` | `False` | "Sure! The monthly plan is $49.99 per month with full access to all our facilities and group classes." | "" | 🟢 PASS |
| `assistant_agent_explicit` | `False` | "Sure! Our monthly plan is $49.99 per month, and it gives you full access to all our gym facilities and group classes." | "" | — |
| `assistant_agent_recast` | `False` | "Sure! The monthly plan is $49.99 per month with full access to all our facilities and classes." | "" | 🟢 PASS |

---

## 📊 第二轮测试总结矩阵 (Summary Matrix)

| 实验组 (Condition) | Recast Purity Pass Rate (重塑纯净通过率) | Explicit Distinctiveness (显性教学对比度) | False Positive Count (误纠错数) | 主要结论 / 问题 (Main Issue) |
| :--- | :---: | :---: | :---: | :--- |
| `dialogue_avatar_explicit` | N/A | 0.0% (0/5) | 0 | 🟢 表现完美，符合预期 |
| `dialogue_avatar_recast` | 100.0% (5/5) | — | 0 | 🟢 表现完美，符合预期 |
| `assistant_agent_explicit` | N/A | 100.0% (5/5) | 0 | ⚠️ 教学特征偏弱，与对话者显性区别较小 |
| `assistant_agent_recast` | 100.0% (5/5) | — | 0 | 🟢 表现完美，符合预期 |


### 📝 状态标注审核
*   `assistant_agent_recast` 是否仍输出 "you mean"：🟢 否
*   Recast 是否仍泄漏 explicit 词：🟢 否
*   Explicit 是否比 Recast 明显更有教学感（包含规则解释）：🟢 是
*   无错误输入是否被误纠错 (False Positive)：🟢 否
