# Vitor Preflight Report

Generated: 2026-05-31 21:46:39
Unity: 6000.3.16f1
Active Build Target: Android
Android Build Support Path: `E:/ProgramFile/UnityEditor/6000.3.16f1/Editor/Data\PlaybackEngines\AndroidPlayer`


## Client Scene

- [x] Main scene exists: `Assets/Scenes/SampleScene.unity`
- [x] Main scene is included in Build Settings
- [x] Active scene is SampleScene

## Demo Rig

- [x] One SceneTalkOrchestrator in scene (found 1)
- [x] One SceneTalkInteractionBootstrap in scene (found 1)
- [x] One SceneTalkVR World UI canvas in scene (found 1)
- [x] One EventSystem in scene (found 1)
- [x] World UI canvas uses World Space render mode
- [x] World UI canvas has an interaction camera
- [x] World UI canvas is not mirrored on Y axis
- [x] World UI canvas has GraphicRaycaster

## Packages

- [x] Input System installed `1.19.0`
- [x] Unity UI installed `2.0.0`
- [x] XR Interaction Toolkit installed `3.5.0`
- [x] OpenXR Plugin installed `1.16.1`
- [x] PICO Unity Integration SDK / PICO XR SDK installed `3.4.0`

## OpenXR

- [x] At least one Android OpenXR interaction profile is enabled
- [x] Android OpenXR has a controller interaction profile enabled

## PICO

- [x] `PICO_OPENXR_SDK` define is set for Android
- [x] Android XR loader uses OpenXR
- [x] PICO XR Support feature is enabled for Android OpenXR
- [x] PICO OpenXR Features extension is enabled
- [x] PICO 4 controller profile is enabled for Android OpenXR

## Android/PICO Build

- [x] Unity Android Build Support module is installed
- [x] Active build target is Android
- [x] Android package id is `com.scenetalkvr.demo`
- [x] Android scripting backend is IL2CPP
- [x] Android target architecture is ARM64
- [x] Android minimum SDK is 29 or higher for PICO

## Manual Steps Still Required

- Run `SceneTalkVR/Setup/Apply Recommended Project Settings` after package import or Unity recompilation.
- If OpenXR validation still reports no interaction profile, run `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile` or add `Khronos Simple Controller Profile` on the Android OpenXR page.
- In Unity Project Settings, keep exactly one Android XR provider path active: OpenXR + PICO features, or PICO native loader.
- Connect PICO 4 with developer mode enabled, then build and run the Android APK.
- Replace demo Spring/Edwin adapters with real LLM, STT, TTS, Avatar, and scene-generation modules.
