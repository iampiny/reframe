# Artwork sources and catalogue design

This chapter preserves the reusable **source research**, not a bundled collection or implemented gallery. The sample records/download variants below were checked during research on **11 October 2026**. Their current availability and rights metadata were **not rechecked on 22 October 2026**. Recheck the exact source record before distribution; a whole website does not have one blanket image license.

The project can use personal uploads and suitably licensed paintings, prints and photography through the native [Art importer](art-mode.md#art-import--native-upload-and-exact-cleanup). It does not require Samsung's paid catalogue. The successful one-image diagnostic does not license unrelated artwork or establish that every candidate will import/display correctly.

## ART-SOURCE-SAMPLE — retained public source records

These are public works/creator credits from the original sample, not personal library titles or raw image/CDN links. Follow the linked record to verify its exact author, attribution and reuse conditions.

| Source record | Creator/credit recorded | Original dimensions recorded | Rights recorded | Suggested app-owned categories |
| --- | --- | --- | --- | --- |
| [Autumn forest by the lake](https://commons.wikimedia.org/wiki/File:Autumn_forest_by_the_lake_(52580612707).jpg) | Aleksandr Gorlov | 4928 × 3264 | [Public Domain Mark](https://creativecommons.org/publicdomain/mark/1.0/) | Photography, Autumn, Nature |
| [Water Lilies](https://commons.wikimedia.org/wiki/File:Claude_Monet_-_Water_Lilies_-_Google_Art_Project.jpg) | Claude Monet | 5610 × 5269 | [Public Domain Mark](https://creativecommons.org/publicdomain/mark/1.0/) | Paintings, Nature |
| [The Great Wave off Kanagawa](https://commons.wikimedia.org/wiki/File:The_Great_Wave_off_Kanagawa.jpg) | After Katsushika Hokusai | 4335 × 2990 | [Public Domain Mark](https://creativecommons.org/publicdomain/mark/1.0/) | Prints, Sea |
| [The Starry Night](https://commons.wikimedia.org/wiki/File:Vincent_van_Gogh_-_Starry_Night_-_Google_Art_Project.jpg) | Vincent van Gogh | 4331 × 3346 | [Public Domain Mark](https://creativecommons.org/publicdomain/mark/1.0/) | Paintings, Night |
| [Countryside near Koryčany](https://commons.wikimedia.org/wiki/File:Czech_Republic_-_landscape_near_Kory%C4%8Dany.jpg) | Pudelek | 4008 × 2672 | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) | Photography, Spring, Nature |
| [Winter in the Tatra Mountains](https://commons.wikimedia.org/wiki/File:Winter_in_Tatry_Mountains_-_Poland.jpg) | Radek Kucharski | 7952 × 4473 | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | Photography, Winter, Nature |

Research downloads included display variants up to **3840 pixels wide** and **960-pixel** thumbnails. These were successful source/download checks, not a physical Art-quality acceptance for every image. Aspect ratio was preserved; 3840 pixels wide does not mean a work fills a 16:9 TV without crop. Original works should stay separate from display/cropped variants.

The original autumn JPEG supplied the bounded native-import fixture. It completed import, new-ID verification and exact cleanup. It was not selected, and Samsung's generated permanent image/thumbnail output was not visually inspected by that diagnostic. The other sample downloads do not inherit that import result.

## ART-SOURCE-LIMITS — museum and image-route findings

- Wikimedia source records and the selected documented thumbnail variants worked for the original sample.
- Art Institute of Chicago metadata API access succeeded, but the tested image endpoint returned **403 in that environment**. This is a historical route failure, not proof that the provider is generally unusable. Its image route remains unvalidated for this project.
- Other museum catalogues, broader image formats/resolutions, automated catalogue refresh and content delivery are candidates, not investigated working integrations documented here.

No live public image fetch or licensing revalidation was performed for this publication pass. Source-page links are retained so contributors can inspect primary records rather than rely on a stale cached label.

## Minimal reusable catalogue record

A gallery should retain enough provenance to show credits and regenerate a display variant. A plain app-owned record is sufficient; Samsung's private database should not be used for these fields.

```text
source record URL
work title and creator credit
license identifier and license URL
original dimensions and source-file digest
display/thumbnail variant dimensions and digests
documented crop/resize/other changes
app-owned categories, such as Autumn or Paintings
native content ID only after a correlated successful import
```

This is a proposed product record, not a shipped catalogue schema. Store personal photo names, local source paths and native photo IDs in app-private data, not public examples or diagnostics. Preserve credit and applicable license information with the item. A Public Domain Mark is a recorded source statement, not a warranty that every jurisdiction or every associated right has been evaluated. CC BY and CC BY-SA items have attribution/other conditions; distribution needs to follow each work's actual license.

Keep custom categories separate from native Art's `MY-C0002` My Photos category. Categories such as season/type can map to lists of imported native IDs without inventing Samsung database records. Keep the original source and offer an explicit fit/matte/crop choice once the relevant rendering contract has been validated. Do not silently crop an artwork and present it as an unmodified source.

The [Art evidence matrix](art-mode.md#evidence-and-current-capability) distinguishes working source/import mechanics from remaining gallery selection, category navigation, matte/crop and physical quality checks.
