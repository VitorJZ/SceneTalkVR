using System;
using System.Collections;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    public interface IAvatarInstanceLoader
    {
        IEnumerator LoadAvatar(
            AvatarResolutionResult resolution,
            Transform parent,
            Action<GameObject> onComplete,
            Action<string> onError);
    }
}
