# 🧪 SceneTalk VR: 纠错提示词快速迭代测试报告 (第 1 轮)

本报告由离线自动化 Prompt 测试脚本生成。测试包含 5 种典型语法偏误的句型，分别在 4 种实验自变量（Provider/Style）配置下，发送至真实大模型服务进行测试。

## 测试用例 1: Subject-Verb Agreement (He go -> He goes)
*   **场景**: `restaurant_reservation` (角色: `barista`)
*   **用户输入**: *"He go to restaurant tomorrow."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Sure, so you'd like a table for tomorrow? And how many people will be in your party?" | "You can say: I'd like to go to the restaurant tomorrow." | — |
| `dialogue_avatar_recast` | `True` | "Oh, you're coming to the restaurant tomorrow? Great! What time and how many people will be in your party?" | "Oh, you're coming to the restaurant tomorrow?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great! So you'd like a table for tomorrow. How many people will be joining you?" | "Grammar tip: Remember to say 'He goes to the restaurant tomorrow' with an 's' on the verb and 'the' before 'restaurant'." | — |
| `assistant_agent_recast` | `True` | "Great, so you're planning to come in tomorrow. How many people will be in your party?" | "You mean he goes to the restaurant tomorrow?" | 🚨 FAIL (Leak: 'you mean') |

---

## 测试用例 2: Adverb Modifier (I very like -> I really like)
*   **场景**: `furniture_shopping` (角色: `clerk`)
*   **用户输入**: *"I very like this furniture."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "I'm glad you like it! Which piece caught your eye?" | "You can say: I really like this furniture." | — |
| `dialogue_avatar_recast` | `True` | "Oh, you really like this furniture? That's great! Which piece caught your eye?" | "Oh, you really like this furniture?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "I'm glad you like it! Which piece caught your eye?" | "Grammar tip: Remember to say 'I really like' instead of 'I very like'." | — |
| `assistant_agent_recast` | `True` | "That's great to hear! What piece caught your eye?" | "You mean you really like this furniture?" | 🚨 FAIL (Leak: 'you mean') |

---

## 测试用例 3: Question Structure (How much cost -> How much does it cost)
*   **场景**: `gym_membership` (角色: `instructor`)
*   **用户输入**: *"How much cost the gym plan?"*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Great question! Our monthly plan is $50, and the yearly plan is $480, which saves you two months." | "You can say: How much does the gym plan cost?" | — |
| `dialogue_avatar_recast` | `True` | "Great question! Our monthly plan is $49, and the yearly plan is $499." | "Oh, you're asking how much the gym plan costs?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great question! Our monthly plan is $50, and the yearly plan is $480, which saves you two months." | "Grammar tip: Remember to say 'How much does the gym plan cost?' with 'does' before the subject." | — |
| `assistant_agent_recast` | `True` | "Our monthly plan is $49, and the yearly plan is $499. Which one sounds better to you?" | "You mean how much does the gym plan cost?" | 🚨 FAIL (Leak: 'you mean') |

---

## 测试用例 4: Missing Article (I have reservation -> I have a reservation)
*   **场景**: `hotel_check_in` (角色: `clerk`)
*   **用户输入**: *"I have reservation under Johnson."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "Great, let me look that up. I see a reservation for Johnson. Could you confirm the spelling of your first name?" | "You can say: I have a reservation under Johnson." | — |
| `dialogue_avatar_recast` | `True` | "Great, let me look that up. I see a reservation under Johnson. Welcome!" | "Great, let me look that up. I see a reservation under Johnson." | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "Great, let me look that up for you. Welcome, Mr. Johnson! I have you down for a deluxe room for two nights. Is that correct?" | "Grammar tip: Remember to include the article 'a' before 'reservation'." | — |
| `assistant_agent_recast` | `True` | "Great, let me look that up. Welcome, Mr. Johnson! I have your reservation right here." | "You mean you have a reservation under Johnson?" | 🚨 FAIL (Leak: 'you mean') |

---

## 测试用例 5: Verb Collocation (make muscle -> build muscle / get in shape)
*   **场景**: `gym_membership` (角色: `instructor`)
*   **用户输入**: *"I want make muscle in gym."*

| 配置项 | hasFeedback | dialogueReply (主对话回复) | feedbackText (音频读词) | Purity Guard 检测 |
| :--- | :---: | :--- | :--- | :---: |
| `dialogue_avatar_explicit` | `True` | "That's great! We have a lot of equipment to help you build muscle. Are you interested in a monthly plan, a yearly plan, or a trial visit?" | "You can say: I want to build muscle in the gym." | — |
| `dialogue_avatar_recast` | `True` | "That's great! We have a full weight room and personal trainers to help you build muscle. Are you interested in a monthly or yearly plan?" | "Oh, you want to build muscle in the gym?" | 🟢 PASS |
| `assistant_agent_explicit` | `True` | "That's great! We have a full range of strength training equipment to help you build muscle. Are you interested in a monthly plan, a yearly plan, or a trial visit?" | "Grammar tip: Remember to say 'I want to build muscle at the gym' instead of 'I want make muscle in gym'." | — |
| `assistant_agent_recast` | `False` | "That's great! We have a full weight room and personal trainers to help you build muscle. Are you interested in a monthly or yearly plan?" | "" | 🟢 PASS |

---

