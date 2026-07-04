# SceneTalkVR Avatar P1 Humanoid Source Log

## Teacher Humanoid v1

- Catalog key: `teacher_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusBusinessMan/teacher_business_man.fbx`
- Source page: https://poly.pizza/m/JFrLIKqvCH
- Source pack: https://poly.pizza/bundle/Ultimate-Modular-Men-Pack-ZiH8muWqwQ
- Creator: Quaternius
- Original model title: `Business Man`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-06-10: `Type=FBX`, `Animated=true`, `Tris=4162`
- Download used: https://static.poly.pizza/e599abbe-7d73-488c-9d7e-3ead281e705c.zip

## Import Notes

- Unity import target: Humanoid rig, Avatar created from this model.
- Humanoid validation: `teacher_business_manAvatar isValid=True isHuman=True` after manual bone mapping.
- Manual mapping note: Quaternius rig has nonstandard leg/foot hierarchy, so `Body` is mapped as Humanoid Hips and `LowerLeg.*_end` is mapped as Humanoid Foot.
- Prefab wrapper normalizes the model to about 1.72 meters tall.
- Prefab wrapper rotates the model 180 degrees around Y so it faces the user from `AvatarRoot`.
- Teacher role readability now uses detached props instead of embedding props in the character prefab.
- Detached book prop prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Props/book_prop_v1.prefab`
- Prop catalog: `Assets/SceneTalkVR/Avatar/Catalogs/AvatarPropCatalog.asset`
- `book_prop_v1` is a lightweight local prop using existing project materials and defaults to the `LeftHand` socket for `teacher`, `instructor`, and `tutor` roles.
- Optional prop layer: `AvatarPropPresenter` can consume the same `SpringScenePayload`, read `AvatarPropCatalog.asset`, and attach props through `AvatarAttachmentSockets` when `AvatarPresentationVoiceModule.attachProps` is explicitly enabled.
- Shared Animator controller: `Assets/SceneTalkVR/Avatar/Animations/Common/AvatarCommonHumanoid.controller`
- Animation mapping: default `Idle_Neutral`, `Think` trigger to `Interact`, `Speak` trigger to `Wave`, and `Talk` trigger to a conservative masked `Rig|Idle_Talking_Loop` gesture layer.
- Runtime trigger layer: `AvatarAnimationDriver` binds the loaded avatar Animator and uses the shared `Think` / `Speak` / `Talk` trigger protocol.
- 2026-06-30 dialogue animation pass: `SceneTalkOrchestrator` marks the first avatar reply in a practice scene as opening speech, so the avatar can keep the greeting wave; later dialogue turns mark replies as follow-up speech and use `Talk`.
- 2026-06-11 idle fix: default idle remains `CharacterArmature|Idle_Neutral`; Idle/Walk/Run clips are imported with loop enabled, and `AvatarPresentationVoiceModule` assigns `AvatarCommonHumanoid.controller` at runtime if a nested FBX Animator loses its controller override.
- 2026-06-30 existing-rig fix: `SceneTalkVR/Setup/Rebuild Demo Rig With Voice Gateway` also rebinds `AvatarPresentationVoiceModule.defaultAnimatorController`, so existing voice-gateway rigs recover animation even when prefab-instantiated Animators lose their controller reference.
- `AvatarCatalog.asset` keeps `teacher_default` as placeholder fallback and adds `teacher_humanoid_v1` as the higher-priority teacher match.

## Masked External Talking Animation

- Source page: https://poly.pizza/m/cwYvO5UauX
- Creator: Quaternius
- Original model title: `Animated Base Character`
- License shown on source page: `Creative Commons Attribution 3.0`
- Source page metadata checked on 2026-06-30: `Type=FBX / GLTF`, `Animated=true`, `Tris=13.7k`
- Download used: https://static.poly.pizza/0b65e14d-a349-44cc-836c-efdeb6933d48.zip
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusAnimatedBaseCharacter/animation_library_unity_standard.fbx`
- Clip used: `Rig|Idle_Talking_Loop`
- Mask asset: `Assets/SceneTalkVR/Avatar/Animations/Common/AvatarTalkGesture.mask`
- Result: full-body retargeting this external `DEF-*` rig produced visible deformation, and the arm/finger portion still looked unnatural when masked to the upper body. The shared controller now uses the clip only on a `Talk Gesture` layer with the Head body part enabled; body, root, legs, arms, and fingers remain on the base idle pose.

## Barista Humanoid v1

- Catalog key: `barista_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/barista_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusAnimatedWoman/barista_animated_woman.fbx`
- Source page: https://poly.pizza/m/nIItLV9nxS
- Creator: Quaternius
- Original model title: `Animated Woman`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-06-10: `Type=FBX`, `Animated=true`, `Tris=3282`
- Download used: https://static.poly.pizza/46d6db5a-3c9f-4238-8cdf-8eb7194498dc.zip

## Frappe Prop v1

- Prop key: `frappe_prop_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Props/frappe_prop_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Props/KenneyFrappe/frappe.obj`
- Source page: https://poly.pizza/m/ZvYPiZeN0V
- Creator: Kenney
- Original model title: `Frappe`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-06-10: `Type=OBJ`, `Animated=false`, `Tris=320`
- Download used: https://static.poly.pizza/a3c0d8bc-884d-41b1-a0b3-1c2c01b65286.zip
- `frappe_prop_v1` defaults to the `RightHand` socket for `barista` and `cafe_worker` roles.
- `AvatarPropPresenter` compensates parent socket scale so props stay near the hand even when imported Humanoid bones carry large local scale.
- 2026-06-11 placement pass: catalog offset was tightened and prop scale was reduced to `0.78`; Play Mode verification measured the prop at about `0.021m` from the Humanoid right hand.
- 2026-06-17 scope pass: props remain in the asset library but are disabled by default for current demos; generated avatars appear without book/frappe unless `attachProps` is explicitly enabled.

## Police Humanoid v1

- Catalog key: `police_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/police_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusSWAT/police_swat.fbx`
- Source page: https://poly.pizza/m/Btfn3G5Xv4
- Creator: Quaternius
- Original model title: `SWAT`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-06-11: `Type=FBX`, `Animated=true`, `Tris=5444`
- Download used: https://static.poly.pizza/713f6535-f4f3-4367-a4c6-ced126ae0936.zip

## Police Import Notes

- Unity import target: Humanoid rig, Avatar created from this model.
- Humanoid validation: `police_swat` prefab Animator Avatar is `isValid=True` and `isHuman=True`.
- Prefab wrapper normalizes the model to about 1.78 meters tall before the Demo Rig `AvatarRoot` scene scale is applied.
- Prefab wrapper rotates the model 180 degrees around Y so it faces the user from `AvatarRoot`.
- `AvatarCatalog.asset` keeps `police_default` as placeholder fallback and adds `police_humanoid_v1` as the higher-priority police/security match.

## Male Barista Humanoid v1

- Catalog key: `barista_male_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/barista_male_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusCasualCharacter/barista_casual_man.fbx`
- Source page: https://poly.pizza/m/kZ3DmIoGip
- Creator: Quaternius
- Original model title: `Casual Character`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-07-04: `Type=FBX`, `Animated=true`, `Tris=3350`
- Download used: https://static.poly.pizza/90a9e2d4-053f-42f1-99a2-8f5e1180ea7f.zip
- FBX used from source pack: `Casual_2.fbx`, renamed to `barista_casual_man.fbx` for stable Unity paths.
- Unity import target: Humanoid rig, Avatar created from this model.
- Unity animation import: disabled for this converted FBX; runtime animation comes from the shared `AvatarCommonHumanoid.controller` and existing stable Humanoid clips.
- Prefab wrapper normalizes the model to about 1.72 meters tall and rotates it 180 degrees around Y so it faces the user from `AvatarRoot`.

## Female Teacher Humanoid v1

- Catalog key: `teacher_female_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/teacher_female_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusSuit/teacher_suit_woman.fbx`
- Source page: https://poly.pizza/m/sOUciDsoVV
- Creator: Quaternius
- Original model title: `Suit`
- License shown on source page: `Creative Commons Attribution`
- Source page metadata checked on 2026-07-04: `Type=FBX`, `Animated=true`, `Tris=3956`
- Download used: https://static.poly.pizza/1bd7759c-ab76-4178-8fe6-7706dffa7d5f.zip
- FBX used from source pack: `Suit.fbx`, renamed to `teacher_suit_woman.fbx` for stable Unity paths.
- Unity import target: Humanoid rig, Avatar created from this model.
- Unity animation import: disabled for this converted FBX; runtime animation comes from the shared `AvatarCommonHumanoid.controller` and existing stable Humanoid clips.
- Prefab wrapper normalizes the model to about 1.66 meters tall and rotates it 180 degrees around Y so it faces the user from `AvatarRoot`.
- Attribution note: keep Quaternius / Poly Pizza attribution in presentation or source documentation.

## Female Police Humanoid v1

- Catalog key: `police_female_humanoid_v1`
- Runtime prefab: `Assets/SceneTalkVR/Avatar/Prefabs/Humanoid/police_female_humanoid_v1.prefab`
- Imported model: `Assets/SceneTalkVR/Avatar/Models/Humanoid/QuaterniusSoldier/police_soldier_woman.fbx`
- Source page: https://poly.pizza/m/oAArCNHjFB
- Creator: Quaternius
- Original model title: `Soldier`
- License shown on source page: `Creative Commons Attribution`
- Source page metadata checked on 2026-07-04: `Type=FBX`, `Animated=true`, `Tris=4400`
- Download used: https://static.poly.pizza/66a55d04-4286-44a3-b289-0d774c27db5b.zip
- FBX used from source pack: `Soldier.fbx`, renamed to `police_soldier_woman.fbx` for stable Unity paths.
- Unity import target: Humanoid rig, Avatar created from this model.
- Unity animation import: disabled for this converted FBX; runtime animation comes from the shared `AvatarCommonHumanoid.controller` and existing stable Humanoid clips.
- Prefab wrapper normalizes the model to about 1.70 meters tall and rotates it 180 degrees around Y so it faces the user from `AvatarRoot`.
- Attribution note: keep Quaternius / Poly Pizza attribution in presentation or source documentation.

## Edwin Boundary

This P1 import changes Unity-side Avatar resource matching, loading, replacement, and fallback. The 2026-07-04 gender pass also updates demo payload parsing, the Unity RealLLM prompt contract, and Avatar-side TTS voice selection so male/female Avatar presets can drive the correct voice alias. It does not implement Spring-side dialogue memory, scene generation, PICO packaging, or real-time lip sync.
