# Required Assets
All third-party content lives in `Assets/ThirdParty/`. Everything below is free (CC0 / OFL / MIT) and can be re-downloaded.

| Asset | Source | License | Location |
|---|---|---|---|
| Quaternius — Ultimate Animated Animal Pack (Alpaca → Camel, White Horse → Arabian Horse, Husky → Saluki, Stag → Arabian Oryx, Deer → Arabian Gazelle, Wolf → Arabian Wolf, Fox → Arabian Fox) | poly.pizza bundle "Animated Animal Pack" by Quaternius (quaternius.com/packs/ultimateanimatedanimals.html) | CC0 | `Assets/ThirdParty/Quaternius/UltimateAnimatedAnimals/` |
| Quaternius — Animal Pack Vol.2 (Eagle → Falcon) | opengameart.org/content/animated-animales-low-poly | CC0 | `Assets/ThirdParty/Quaternius/AnimalPackVol2/` |
| Fredoka font (SemiBold/Bold static instances) | Google Fonts (github.com/google/fonts, ofl/fredoka) | SIL OFL 1.1 | `Assets/ThirdParty/GoogleFonts/Fredoka/` |
| Tajawal font (Medium, Bold) — the Arabic font, with a static TMP font asset covering every Arabic letter, joined forms and digits | Google Fonts (github.com/google/fonts, ofl/tajawal), by Boutros International | SIL OFL 1.1 | `Assets/ThirdParty/GoogleFonts/Tajawal/`, TMP assets in `Assets/_Project/Fonts/` |
| RTLTMPro (Arabic letter joining + right-to-left text for TextMeshPro), runtime scripts only | github.com/pnarimani/RTLTMPro (commit f480419), by Mohamad Narimani | MIT | `Assets/ThirdParty/RTLTMPro/` |
| Music "Darbuka Delight" by Adiutorium (menu + map) and "blow the day with a darbuka + chase (Adiutorium remixed)" (battle) | opengameart.org (re-encoded to Ogg Vorbis, 2 s fade-out) | CC0 | `Assets/ThirdParty/OpenGameArt/Audio/Music_*.ogg` |
| Kenney "Interface Sounds" (click, win, lose, pop) | opengameart.org/content/interface-sounds | CC0 | `Assets/ThirdParty/OpenGameArt/Audio/Sfx_*.ogg` |
| "Impact" pack by StarNinjas (hit, critical hit, guard) | opengameart.org/content/10-impactshield-blocks | CC0 | `Assets/ThirdParty/OpenGameArt/Audio/Sfx_Hit/Crit/Guard.ogg` |
| TextMesh Pro Essential Resources | Unity (com.unity.ugui package) | Unity | `Assets/TextMesh Pro/` |

Per-model source URLs: `Assets/ThirdParty/Quaternius/LICENSE.txt`. Audio details: `Assets/ThirdParty/OpenGameArt/Audio/README.txt`.
No free low-poly Camel / Falcon / Saluki / Oryx / Gazelle exists in a matching animated style, so the closest model is recolored and given extras by `AnimalAssetBuilder` (camel hump, oryx + gazelle horns, big fox ears, shorter falcon wings).
Tajawal has no glyphs for the "isolated" Arabic presentation forms (U+FE8D…) that RTLTMPro writes; `ArabicFontBuilder` aliases them to the base letters' glyphs (same shape) when it builds the TMP font assets.
UPM packages (auto-restore from `Packages/manifest.json`): URP 17.3, Input System 1.18, glTFast 6.20 (needed to import the .glb animals), Unity Localization 1.5.13 (+ Addressables), Cinemachine, ProBuilder, AI Navigation, MCP for Unity.
Raw downloads (not imported) are kept in `_downloads/` at the project root — safe to delete.
