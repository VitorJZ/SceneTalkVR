# Correction Assistant Humanoid Source Log

- Runtime role: embodied correction assistant for the pilot experiment
- Source page: https://poly.pizza/m/qJ2gsTUBHL
- Public ID: `qJ2gsTUBHL`
- Resource ID: `ba7a1955-ea51-4cb9-a561-188bdef0a6c7`
- Creator: Quaternius
- Original model title: `Animated Woman`
- Source archive file: `Casual.fbx`
- License shown on source page: `CC0 1.0` / Public Domain
- Source page metadata checked on 2026-07-17: `Type=FBX`, `Animated=true`, `Tris=3410`
- Download used: https://static.poly.pizza/ba7a1955-ea51-4cb9-a561-188bdef0a6c7.zip

## Import Policy

- Keep this model separate from the existing `barista_humanoid_v1`; they are different Poly Pizza resources.
- Import as a Unity Humanoid with an Avatar created from this model.
- Normalize the correction assistant wrapper to about 1.68 meters tall and disable root motion.
- Reuse `AvatarCommonHumanoid.controller` through a dedicated override controller.
- Use the model's native `Idle_Neutral` loop and `Wave` override, while normal correction speech uses the shared upper-body `TalkLoop` through `IsTalking`.
- Do not add the assistant to `AvatarCatalog.asset`; it is assigned directly to `CorrectionAgentPresenter`.
