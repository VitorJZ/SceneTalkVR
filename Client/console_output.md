[HolodeckSceneService] Requesting 3D layout for: coffee_shop
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HolodeckSceneService/<GenerateLayoutAsync>d__3:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HolodeckSceneService.cs:30)
System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1<SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse>:Start<SceneTalkVR.Runtime.Services.HolodeckSceneService/<GenerateLayoutAsync>d__3> (SceneTalkVR.Runtime.Services.HolodeckSceneService/<GenerateLayoutAsync>d__3&)
SceneTalkVR.Runtime.Services.HolodeckSceneService:GenerateLayoutAsync (string)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:55)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HolodeckSceneService] Layout received: {"environment":"coffee_shop","objects":[{"name":"counter-0","position":[5.758,0.218,4.9],"rotation":270.0},{"name":"communal","position":[4.9,0.358,4.9],"rotation":90.0},{"name":"wall","position":[5.927,2.026,4.9],"rotation":270.0}]}
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HolodeckSceneService/<GenerateLayoutAsync>d__3:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HolodeckSceneService.cs:55)
UnityEngine.UnitySynchronizationContext:ExecuteTasks ()

[PanoramaSceneService] Background applied successfully.
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.PanoramaSceneService:ApplySkybox (UnityEngine.Texture2D) (at Assets/SceneTalkVR/Scripts/Services/PanoramaSceneService.cs:218)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:66)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HybridScenePresenter] Received 3 objects from backend.
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HybridScenePresenter:InstantiateHolodeckObjects (SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse) (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:106)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:76)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HybridScenePresenter] Auto-centering scene. Applied offset: (-5.53, 0.00, -4.90)
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HybridScenePresenter:InstantiateHolodeckObjects (SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse) (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:125)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:76)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HybridScenePresenter] Skipping 'coffee_counter' (Original: 'counter-0') - not in whitelist.
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HybridScenePresenter:InstantiateHolodeckObjects (SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse) (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:141)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:76)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HybridScenePresenter] Skipping 'generic_decor' (Original: 'communal') - not in whitelist.
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HybridScenePresenter:InstantiateHolodeckObjects (SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse) (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:141)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:76)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[HybridScenePresenter] Skipping 'generic_decor' (Original: 'wall') - not in whitelist.
UnityEngine.Debug:Log (object)
SceneTalkVR.Runtime.Services.HybridScenePresenter:InstantiateHolodeckObjects (SceneTalkVR.Runtime.Services.HolodeckSceneService/HolodeckResponse) (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:141)
SceneTalkVR.Runtime.Services.HybridScenePresenter/<PresentScene>d__15:MoveNext () (at Assets/SceneTalkVR/Scripts/Services/HybridScenePresenter.cs:76)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[SceneTalkVR] Avatar resolved: key=barista_humanoid_v1, score=210, fallback=exact_or_close
UnityEngine.Debug:Log (object,UnityEngine.Object)
SceneTalkVR.AvatarSystem.AvatarPresentationVoiceModule/<EnsureAvatar>d__34:MoveNext () (at Assets/SceneTalkVR/Avatar/Scripts/AvatarPresentationVoiceModule.cs:210)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

