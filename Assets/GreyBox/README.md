# Grey-Box: Teich & Aquarium

Alles in diesem Ordner baut der Menüpunkt **MCI > Grey-Box erzeugen** in die offene Szene
(`Assets/Scenes/Main.unity`). Änderungen an Größen, Positionen oder Farben gehören in
`Editor/GreyBoxBuilder.cs`, danach den Menüpunkt erneut ausführen.

## Benötigte Asset-Store-Pakete (kostenlos, jeder importiert sie selbst)

Die Lizenz des Unity Asset Store erlaubt nicht, diese Dateien in ein öffentliches Repo zu laden.
Darum stehen sie in `.gitignore`. Ohne sie baut der Builder einfache Formen als Ersatz.

1. Auf der Asset-Store-Seite "Add to My Assets" klicken:
   - Fish Alive - Free Samples (Guppy, Clownfisch): https://assetstore.unity.com/packages/3d/characters/animals/fish/fish-alive-free-samples-362806
   - (PBR) Low-Grow Flora (Pflanzen): https://assetstore.unity.com/packages/3d/vegetation/plants/pbr-low-grow-flora-212217
   - Stones (Steine): https://assetstore.unity.com/packages/3d/props/exterior/stones-40329
2. In Unity: Window > Package Management > Package Manager > My Assets > Paket wählen > Download > Import.
3. MCI > Grey-Box erzeugen. Der Builder stellt die Materialien von Pflanzen und Steinen automatisch auf URP um.

## Weitere Modelle

- `Models/FishingRod_Quaternius.glb`: "Fishing Rod" von Quaternius, CC0 (siehe `Models/LIZENZEN.txt`).
  Braucht das Paket glTFast (`com.unity.cloud.gltfast`, steht schon in `Packages/manifest.json`).

## Was die Skripte machen

- `Placeable`: Teich, Aquarium und Kiste an den weißen Griffen tragen, mit zwei Händen skalieren,
  beim Loslassen auf Boden oder Tisch einrasten (Tiefensensor der Quest 3), Inhalt wandert mit.
- `HandleHighlight`: Griffe werden hellblau, wenn eine Hand nah ist, und blau beim Greifen.
- `ContentZone`: Innenraum von Teich, Aquarium und Kiste; im Wasser schweben Fische, der Rest sinkt.
- `FishSwimmer`: Fish-Alive-Fische schwimmen im Wasser selbst herum, gegriffen oder an Land nicht.
- `GreyBoxItem`: alles Greifbare; kehrt zurück, wenn es durch den Boden fällt.
- `SpawnInFrontOfUser`: stellt die Grey-Box beim Start vor dich.
