# Panorama generation and migration report

Status: `BLOCKED_GENERATOR_NOT_CAPABLE`.

The repository's SiliconFlow implementation calls `/v1/images/generations` with model `Tongyi-MAI/Z-Image` and `image_size=1024x1024`. Adding “360 degree equirectangular” to a text prompt does not establish native spherical/equirectangular output. The service path exposes no verified 2:1 panorama mode or projection metadata. Therefore this change does not call it to replace formal assets and does not stretch, crop, or relabel square images.

No legacy file was archived because no valid replacement was produced. The four square resources remain available for compatibility but fail the hard validator. Tourist is 2048×1024, but lacks native equirectangular provenance and is not claimed collection-ready.

`PanoramaAssetValidator` adds existence, Resources load, dimensions/aspect, provenance, catalog-reference, importer, Android memory, and left/right edge checks. `SceneTalkVR/Diagnostics/Panorama Preview` provides a five-resource preview. `Apply Panorama Import Contract` applies Default/2D/sRGB/no alpha/Repeat/Trilinear/mipmaps/max 4096 and Android ASTC 6×6/max 4096/quality 100 without changing pixels.

To unblock, use a provider/tool that explicitly returns native equirectangular 2:1 imagery (preferred 4096×2048), retain original output and prompt/provider/model/timestamp, add a provenance sidecar, visually verify poles and seam, then archive replaced legacy images under `Assets/SceneTalkVR/ArtArchive/PanoramaLegacy/`.
