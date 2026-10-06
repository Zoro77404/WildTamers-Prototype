# Required Assets
All third-party content lives in `Assets/ThirdParty/`. Everything below is free (CC0 / OFL) and can be re-downloaded.

| Asset | Source | License | Location |
|---|---|---|---|
| Quaternius — Ultimate Animated Animal Pack (Alpaca → Camel, White Horse → Arabian Horse, Husky → Saluki, Stag → Arabian Oryx, Deer → Arabian Gazelle, Wolf → Arabian Wolf, Fox → Arabian Fox) | poly.pizza bundle "Animated Animal Pack" by Quaternius (quaternius.com/packs/ultimateanimatedanimals.html) | CC0 | `Assets/ThirdParty/Quaternius/UltimateAnimatedAnimals/` |
| Quaternius — Animal Pack Vol.2 (Eagle → Falcon) | opengameart.org/content/animated-animales-low-poly | CC0 | `Assets/ThirdParty/Quaternius/AnimalPackVol2/` |
| Fredoka font (SemiBold/Bold static instances) | Google Fonts (github.com/google/fonts, ofl/fredoka) | SIL OFL 1.1 | `Assets/ThirdParty/GoogleFonts/Fredoka/` |
| TextMesh Pro Essential Resources | Unity (com.unity.ugui package) | Unity | `Assets/TextMesh Pro/` |

Per-model source URLs: `Assets/ThirdParty/Quaternius/LICENSE.txt`.
No free low-poly Camel / Falcon / Saluki / Oryx / Gazelle exists in a matching animated style, so the closest model is recolored and given extras by `AnimalAssetBuilder` (camel hump, oryx + gazelle horns, big fox ears, shorter falcon wings).
UPM packages (auto-restore from `Packages/manifest.json`): URP 17.3, Input System 1.18, glTFast 6.20 (needed to import the .glb animals), Cinemachine, ProBuilder, AI Navigation, MCP for Unity.
Raw downloads (not imported) are kept in `_downloads/` at the project root — safe to delete.
