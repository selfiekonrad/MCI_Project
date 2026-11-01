using MCI.GreyBox;
using Meta.XR;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Baut den Grey-Box-Prototyp in die offene Szene. Menü: MCI > Grey-Box erzeugen.
///
/// Teich, Aquarium und Deko-Kiste haben weiße Griffe: tragen, drehen, mit zwei Händen
/// größer/kleiner ziehen, loslassen → rastet auf Boden oder Tisch ein (siehe Placeable).
/// Greifbare Objekte und Griffe werden vom vorhandenen "[BuildingBlock] Cube" kopiert,
/// damit sie genau dieselbe Hand-Grab-Einrichtung des Meta SDK haben.
/// Koordinaten: Ursprung = Boden an deiner Startposition, +Z = Blickrichtung, Einheit Meter.
/// </summary>
public static class GreyBoxBuilder
{
    const string RootName = "GreyBox";
    const string TemplateName = "[BuildingBlock] Cube";
    const string MaterialFolder = "Assets/GreyBox/Materials";
    const string MainScene = "Assets/Scenes/Main.unity";
    const string RodModel = "Assets/GreyBox/Models/FishingRod_Quaternius.glb";

    // Asset-Store-Pakete (jeder im Team importiert sie selbst, siehe Assets/GreyBox/README.md).
    // Fehlen sie, baut der Builder einfache Formen als Ersatz.
    const string Guppy = "Assets/DenysAlmaral/FishAlive/Prefabs/FishFreshwater/freshWater_guppy.prefab";
    const string Clownfish = "Assets/DenysAlmaral/FishAlive/Prefabs/FishMarine/marine_clownfish.prefab";
    const string Stones = "Assets/Free/Stones/Mesh/";
    const string Flora = "Assets/(PBR) Low-Grow Flora/Prefabs/Med Res/";

    enum Fit { AlongY, Height, Longest }

    static GameObject _template;
    static Material _grey, _dark, _pondWater, _tankWater, _glass, _edge, _wood, _handle, _fish, _stone, _plant, _rod;

    [MenuItem("MCI/Grey-Box erzeugen")]
    public static void BuildMenu()
    {
        if (Build()) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    [MenuItem("MCI/Grey-Box entfernen")]
    public static void RemoveMenu()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    // Für Unity im Batch-Modus: -executeMethod GreyBoxBuilder.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        var scene = EditorSceneManager.OpenScene(MainScene);
        if (!Build()) EditorApplication.Exit(1);
        EditorSceneManager.SaveScene(scene);
    }

    static bool Build()
    {
        _template = FindTemplate();
        if (_template == null)
        {
            Debug.LogError($"Grey-Box: '{TemplateName}' nicht in der Szene gefunden. Füge zuerst über Meta > Tools > Building Blocks den Block 'Grabbable Item' hinzu.");
            return false;
        }
        // Der Original-Würfel dient nur als Vorlage und wird im Spiel ausgeblendet
        _template.SetActive(false);

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Grey-Box erzeugen");
        root.gameObject.AddComponent<SpawnInFrontOfUser>();

        _grey = Mat("Grau", new Color(0.55f, 0.55f, 0.55f));
        _dark = Mat("Dunkelgrau", new Color(0.3f, 0.3f, 0.3f));
        _pondWater = Mat("Wasser", new Color(0.35f, 0.6f, 0.9f, 0.35f), true, 0.9f);
        _tankWater = Mat("Aquarium-Wasser", new Color(0.6f, 0.82f, 0.95f, 0.1f), true, 0.9f);
        _glass = Mat("Glas", new Color(0.94f, 0.98f, 1f, 0.1f), true, 0.97f);
        _edge = Mat("Kante", new Color(0.06f, 0.06f, 0.07f), false, 0.35f);
        _wood = Mat("Holz", new Color(0.55f, 0.38f, 0.22f));
        _handle = Mat("Griff", new Color(0.95f, 0.95f, 0.95f), false, 0.6f);
        _fish = Mat("Fisch", new Color(1f, 0.55f, 0.15f));
        _stone = Mat("Stein", new Color(0.45f, 0.42f, 0.4f));
        _plant = Mat("Pflanze", new Color(0.3f, 0.7f, 0.3f));
        _rod = Mat("Rute", new Color(0.6f, 0.4f, 0.2f));

        // Unsichtbarer Boden, damit nichts ins Leere fällt (Tracking-Ursprung = echter Boden)
        var floor = new GameObject("Boden (unsichtbar)");
        floor.transform.SetParent(root, false);
        floor.transform.localPosition = new Vector3(0, -0.05f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(20, 0.1f, 20);

        SetUpSurfaceDetection(root);

        // Teich liegt auf dem Boden, Aquarium und Kiste schweben auf Tischhöhe, bis man sie abstellt
        var pond = BuildPond(root, new Vector3(-0.7f, 0f, 1.4f));
        BuildAquarium(root, new Vector3(0.6f, 0.75f, 1.0f));
        var crate = BuildCrate(root, new Vector3(0.45f, 0.75f, 0.45f));

        // --- Greifbare Objekte ---
        var items = new GameObject("Greifbar").transform;
        items.SetParent(root, false);
        var lying = Quaternion.Euler(0, 0, 90);

        // Fische schwimmen im Teich (Fish Alive), sonst Kapseln als Ersatz
        Fish(items, pond, "Guppy klein", Guppy, 0.11f, new Vector3(-0.15f, 0.1f, -0.1f), 30f, new Vector3(0.03f, 0.04f, 0.03f));
        Fish(items, pond, "Guppy", Guppy, 0.135f, new Vector3(0.12f, 0.14f, 0.15f), 160f, new Vector3(0.04f, 0.06f, 0.04f));
        Fish(items, pond, "Clownfisch klein", Clownfish, 0.13f, new Vector3(0.15f, 0.09f, -0.15f), 250f, new Vector3(0.05f, 0.065f, 0.05f));
        Fish(items, pond, "Clownfisch", Clownfish, 0.165f, new Vector3(-0.12f, 0.13f, 0.18f), 90f, new Vector3(0.06f, 0.08f, 0.06f));

        // Steine und Pflanzen liegen in der Deko-Kiste
        Stone(items, crate, "Stein klein", Stones + "Stone_1.fbx", 0.05f, new Vector3(-0.15f, 0.01f, -0.06f), new Vector3(0.04f, 0.03f, 0.04f));
        Stone(items, crate, "Stein mittel", Stones + "Stone_3.fbx", 0.07f, new Vector3(-0.1f, 0.01f, 0.05f), new Vector3(0.07f, 0.05f, 0.06f));
        Stone(items, crate, "Stein gross", Stones + "Stone_4.fbx", 0.1f, new Vector3(-0.03f, 0.01f, -0.04f), new Vector3(0.1f, 0.07f, 0.08f));
        Plant(items, crate, "Pflanze Rosette", Flora + "Diamond Cabbage A1.prefab", 0.08f, new Vector3(0.07f, 0.01f, -0.06f), new Vector3(0.05f, 0.04f, 0.05f));
        Plant(items, crate, "Pflanze Gras", Flora + "Fade Root Cabbage A1.prefab", 0.14f, new Vector3(0.11f, 0.01f, 0.06f), new Vector3(0.02f, 0.07f, 0.02f));
        Plant(items, crate, "Pflanze Staengel", Flora + "Worthweed 1.prefab", 0.2f, new Vector3(0.15f, 0.01f, -0.05f), new Vector3(0.03f, 0.1f, 0.03f));

        BuildRod(items, new Vector3(-0.05f, 0.9f, 0.9f), Quaternion.Euler(80, 0, 0));

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        Debug.Log("Grey-Box erzeugt.");
        return true;
    }

    // ---------- Teich, Aquarium, Kiste, Angel ----------

    /// Runder Teich als Becken: 1 m Durchmesser, 25 cm hoch, die Fische schwimmen im Wasser darin.
    static Transform BuildPond(Transform root, Vector3 pos)
    {
        const float r = 0.5f, h = 0.25f, wall = 0.04f, bottom = 0.02f, waterTop = 0.215f;
        const int segments = 24;
        var (pond, body, grips) = NewPlaceable(root, "Teich", pos, 0.5f, 2f);

        var floor = Prim(PrimitiveType.Cylinder, "Boden", body, _dark);
        floor.transform.localPosition = new Vector3(0, bottom / 2, 0);
        floor.transform.localScale = new Vector3(2 * r, bottom / 2, 2 * r);

        // Rand aus 24 Segmenten, damit das Becken innen hohl ist
        var rim = new GameObject("Rand").transform;
        rim.SetParent(body, false);
        var segmentWidth = 2f * r * Mathf.Tan(Mathf.PI / segments) * 1.08f;
        for (int i = 0; i < segments; i++)
        {
            var a = i * 2f * Mathf.PI / segments;
            var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var segment = Prim(PrimitiveType.Cube, $"Segment {i + 1}", rim, _grey);
            segment.transform.localPosition = radial * (r - wall / 2) + Vector3.up * (h / 2);
            segment.transform.localRotation = Quaternion.LookRotation(radial, Vector3.up);
            segment.transform.localScale = new Vector3(segmentWidth, h, wall);
        }

        var inner = 2 * (r - wall);
        var water = Prim(PrimitiveType.Cylinder, "Wasser", body, _pondWater, collider: false);
        water.transform.localPosition = new Vector3(0, (bottom + waterTop) / 2, 0);
        water.transform.localScale = new Vector3(inner, (waterTop - bottom) / 2, inner);

        // Schwimmbereich: Quadrat im Kreis, zwischen Boden und Wasseroberfläche
        var side = inner / Mathf.Sqrt(2f);
        BoxZone("Wasser-Zone", body, new Vector3(0, (bottom + waterTop) / 2, 0), new Vector3(side, waterTop - bottom - 0.01f, side), true);

        // Zwei schlanke weiße Stäbe am Rand, oben 15 cm Griff (bis 45 cm Höhe),
        // damit man sich zum Tragen nicht ganz bücken muss
        foreach (var s in new[] { -1f, 1f })
        {
            var x = s * (r + 0.06f);
            var stem = Prim(PrimitiveType.Cylinder, s < 0 ? "Stab links" : "Stab rechts", grips, _handle, collider: false);
            stem.transform.localPosition = new Vector3(x, 0.15f, 0);
            stem.transform.localScale = new Vector3(0.008f, 0.15f, 0.008f);
            Handle(grips, s < 0 ? "Griff links" : "Griff rechts", new Vector3(x, 0.375f, 0), Vector3.up, 0.15f);
        }
        return pond;
    }

    /// Randloses Glasaquarium 60 x 35 x 30 cm, oben offen, mit dunklen Kanten.
    static void BuildAquarium(Transform root, Vector3 pos)
    {
        const float w = 0.6f, h = 0.35f, d = 0.3f, t = 0.006f, e = 0.005f;
        var (_, body, grips) = NewPlaceable(root, "Aquarium", pos, 0.6f, 2f);

        // Glasscheiben (mit Kollision)
        Box(body, "Glas Boden", _glass, new Vector3(0, t / 2, 0), new Vector3(w, t, d));
        Box(body, "Glas vorne", _glass, new Vector3(0, h / 2, -(d - t) / 2), new Vector3(w, h, t));
        Box(body, "Glas hinten", _glass, new Vector3(0, h / 2, (d - t) / 2), new Vector3(w, h, t));
        Box(body, "Glas links", _glass, new Vector3(-(w - t) / 2, h / 2, 0), new Vector3(t, h, d - 2 * t));
        Box(body, "Glas rechts", _glass, new Vector3((w - t) / 2, h / 2, 0), new Vector3(t, h, d - 2 * t));

        // Dunkle Linien an allen zwölf Kanten (nur Optik)
        var edges = new GameObject("Kanten").transform;
        edges.SetParent(body, false);
        float xe = w / 2 - e / 2, ze = d / 2 - e / 2;
        foreach (var x in new[] { -xe, xe })
            foreach (var z in new[] { -ze, ze })
                Line(edges, "Kante senkrecht", new Vector3(x, h / 2, z), new Vector3(e, h, e));
        foreach (var y in new[] { e / 2, h - e / 2 })
        {
            foreach (var z in new[] { -ze, ze })
                Line(edges, "Kante quer", new Vector3(0, y, z), new Vector3(w, e, e));
            foreach (var x in new[] { -xe, xe })
                Line(edges, "Kante tief", new Vector3(x, y, 0), new Vector3(e, e, d));
        }

        var waterH = h * 0.85f;
        var inner = new Vector3(w - 2 * t, waterH, d - 2 * t);
        var waterPos = new Vector3(0, t + waterH / 2, 0);
        var water = Prim(PrimitiveType.Cube, "Wasser", body, _tankWater, collider: false);
        water.transform.localPosition = waterPos;
        water.transform.localScale = inner;
        BoxZone("Wasser-Zone", body, waterPos, inner, true);

        // Weiße Griff-Linien außen an den Seiten, waagrecht
        Handle(grips, "Griff links", new Vector3(-(w / 2 + 0.03f), h * 0.8f, 0), Vector3.forward, 0.18f);
        Handle(grips, "Griff rechts", new Vector3(w / 2 + 0.03f, h * 0.8f, 0), Vector3.forward, 0.18f);
    }

    static Transform BuildCrate(Transform root, Vector3 pos)
    {
        // Offene Holzkiste für Steine und Pflanzen: 40 x 12 x 25 cm
        const float w = 0.4f, h = 0.12f, d = 0.25f, t = 0.01f;
        var (crate, body, grips) = NewPlaceable(root, "Deko-Kiste", pos, 0.6f, 2f);

        Box(body, "Boden", _wood, new Vector3(0, t / 2, 0), new Vector3(w, t, d));
        Box(body, "Wand vorne", _wood, new Vector3(0, h / 2, -(d - t) / 2), new Vector3(w, h, t));
        Box(body, "Wand hinten", _wood, new Vector3(0, h / 2, (d - t) / 2), new Vector3(w, h, t));
        Box(body, "Wand links", _wood, new Vector3(-(w - t) / 2, h / 2, 0), new Vector3(t, h, d - 2 * t));
        Box(body, "Wand rechts", _wood, new Vector3((w - t) / 2, h / 2, 0), new Vector3(t, h, d - 2 * t));
        BoxZone("Inhalt", body, new Vector3(0, t + 0.1f, 0), new Vector3(w - 2 * t, 0.2f, d - 2 * t), false);

        Handle(grips, "Griff links", new Vector3(-(w / 2 + 0.03f), h * 0.7f, 0), Vector3.forward, 0.16f);
        Handle(grips, "Griff rechts", new Vector3(w / 2 + 0.03f, h * 0.7f, 0), Vector3.forward, 0.16f);
        return crate;
    }

    /// Angel (1,2 m): Modell von Quaternius, falls vorhanden, sonst Zylinder mit Griff.
    /// Schwebt (kinematisch) auf Hüfthöhe und bleibt dort, wo man sie loslässt.
    static void BuildRod(Transform items, Vector3 pos, Quaternion rot)
    {
        const float length = 1.2f;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RodModel);
        if (model == null)
        {
            var r = Grab(items, items, "Rute (1,2 m)", PrimitiveType.Cylinder, new Vector3(0.02f, length / 2, 0.02f), pos, rot, _rod, GreyBoxItem.Kind.Rute, physics: false);
            var handle = Prim(PrimitiveType.Cylinder, "Griff", r.transform, _dark);
            handle.transform.localPosition = new Vector3(0, -0.75f, 0);
            handle.transform.localScale = new Vector3(2f, 0.25f, 2f); // 4 cm dick, 30 cm lang
            return;
        }

        var go = ModelItem(items, "Rute (1,2 m)", pos, rot);
        FitModel(go.transform, model, length, Fit.AlongY);
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.height = length;
        capsule.radius = 0.02f;
        SetUpItem(go, items, GreyBoxItem.Kind.Rute, physics: false);
    }

    /// Fisch aus Fish Alive, der im Wasser selbst schwimmt (FishSwimmer). Ohne Paket: Kapsel.
    static void Fish(Transform items, Transform home, string name, string prefabPath, float length, Vector3 local, float yaw, Vector3 fallbackScale)
    {
        var pos = home.TransformPoint(local);
        var rot = home.rotation * Quaternion.Euler(0, yaw, 0);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (model == null)
        {
            Grab(items, home, name, PrimitiveType.Capsule, fallbackScale, pos, rot * Quaternion.Euler(90, 0, 0), _fish, GreyBoxItem.Kind.Fisch);
            return;
        }

        var go = ModelItem(items, name, pos, rot);
        var b = FitModel(go.transform, model, length, Fit.Longest);
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.direction = 2; // Fische schauen entlang Z
        capsule.height = b.size.z;
        capsule.radius = Mathf.Max(0.02f, Mathf.Max(b.size.x, b.size.y) / 2f);
        SetUpItem(go, home, GreyBoxItem.Kind.Fisch, physics: true);

        var swimmer = go.AddComponent<FishSwimmer>();
        swimmer.model = go.transform.Find("Modell");
        foreach (var animator in go.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
    }

    /// Stein aus dem Paket "Stones", Kollision passend zur Form. Ohne Paket: flache Kugel.
    static void Stone(Transform items, Transform home, string name, string modelPath, float size, Vector3 local, Vector3 fallbackScale)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Grab(items, home, name, PrimitiveType.Sphere, fallbackScale, home.TransformPoint(local + Vector3.up * (fallbackScale.y / 2 + 0.005f)), home.rotation, _stone, GreyBoxItem.Kind.Stein);
            return;
        }

        UpgradeToUrp(model);
        var go = ModelItem(items, name, home.position, home.rotation);
        var b = FitModel(go.transform, model, size, Fit.Longest);
        go.transform.position = home.TransformPoint(local + Vector3.up * (b.extents.y + 0.005f));
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            mf.gameObject.AddComponent<MeshCollider>().convex = true;
        SetUpItem(go, home, GreyBoxItem.Kind.Stein, physics: true);
    }

    /// Pflanze aus "Low-Grow Flora", schlanke Kollision in der Mitte. Ohne Paket: Zylinder.
    static void Plant(Transform items, Transform home, string name, string prefabPath, float height, Vector3 local, Vector3 fallbackScale)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (model == null)
        {
            Grab(items, home, name, PrimitiveType.Cylinder, fallbackScale, home.TransformPoint(local + Vector3.up * (fallbackScale.y + 0.005f)), home.rotation, _plant, GreyBoxItem.Kind.Pflanze);
            return;
        }

        UpgradeToUrp(model);
        var go = ModelItem(items, name, home.position, home.rotation);
        var b = FitModel(go.transform, model, height, Fit.Height);
        go.transform.position = home.TransformPoint(local + Vector3.up * (b.extents.y + 0.005f));
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.height = b.size.y;
        capsule.radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) / 4f, 0.015f, 0.05f);
        SetUpItem(go, home, GreyBoxItem.Kind.Pflanze, physics: true);
    }

    /// Greifbares Objekt ohne eigene Form (die kommt aus einem Modell): Kopie der Vorlage ohne Würfel.
    static GameObject ModelItem(Transform parent, string name, Vector3 pos, Quaternion rot)
    {
        var go = CloneTemplate(parent, name);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.one;
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        return go;
    }

    /// Asset-Store-Pakete für die alte Render-Pipeline: Materialien auf URP umstellen (sonst rosa).
    static void UpgradeToUrp(GameObject model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || (m.shader.name != "Standard" && m.shader.name != "Standard (Specular setup)")) continue;
                UnityEditor.Rendering.MaterialUpgrader.Upgrade(m, new UnityEditor.Rendering.Universal.StandardUpgrader(m.shader.name),
                    UnityEditor.Rendering.MaterialUpgrader.UpgradeFlags.None);
                EditorUtility.SetDirty(m);
            }
    }

    /// Leeres Platzier-Objekt: "Körper" (Wände, Wasser) und "Griffe" (greifbar, bewegt das Ganze).
    /// Nur die Griffe sind greifbar, damit man beim Fische-Greifen nicht aus Versehen den Teich packt.
    static (Transform place, Transform body, Transform grips) NewPlaceable(Transform root, string name, Vector3 pos, float minScale, float maxScale)
    {
        var place = new GameObject(name).transform;
        place.SetParent(root, false);
        place.localPosition = pos;

        var body = new GameObject("Körper").transform;
        body.SetParent(place, false);
        var bodyRb = body.gameObject.AddComponent<Rigidbody>();
        bodyRb.isKinematic = true;
        bodyRb.useGravity = false;

        var grips = CloneTemplate(place, "Griffe");
        Object.DestroyImmediate(grips.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(grips.GetComponent<MeshFilter>());
        foreach (var c in grips.GetComponents<Collider>()) Object.DestroyImmediate(c);
        grips.transform.localPosition = Vector3.zero;
        grips.transform.localRotation = Quaternion.identity;
        grips.transform.localScale = Vector3.one;

        var rb = grips.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Eine Hand: verschieben + um die Hochachse drehen. Zwei Hände: zusätzlich skalieren.
        var transformer = grips.AddComponent<GrabFreeTransformer>();
        var locked = new TransformerUtils.ConstrainedAxis { ConstrainAxis = true, AxisRange = new TransformerUtils.FloatRange { Min = 0, Max = 0 } };
        transformer.InjectOptionalRotationConstraints(new TransformerUtils.RotationConstraints
        {
            XAxis = locked,
            YAxis = TransformerUtils.ConstrainedAxis.Unconstrained,
            ZAxis = locked
        });
        var range = new TransformerUtils.ConstrainedAxis { ConstrainAxis = true, AxisRange = new TransformerUtils.FloatRange { Min = minScale, Max = maxScale } };
        transformer.InjectOptionalScaleConstraints(new TransformerUtils.ScaleConstraints
        {
            ConstraintsAreRelative = true,
            XAxis = range,
            YAxis = range,
            ZAxis = range
        });

        var grabbable = grips.GetComponent<Grabbable>();
        grabbable.InjectOptionalTargetTransform(place);
        grabbable.InjectOptionalOneGrabTransformer(transformer);
        grabbable.InjectOptionalTwoGrabTransformer(transformer);
        grabbable.InjectOptionalThrowWhenUnselected(false);
        grips.AddComponent<HandleHighlight>();

        place.gameObject.AddComponent<Placeable>().handles = grabbable;
        return (place, body, grips.transform);
    }

    /// Weißer Griff als schlanke Linie (1,4 cm dick) mit großzügiger, unsichtbarer Greifzone drumherum.
    static void Handle(Transform grips, string name, Vector3 pos, Vector3 axis, float length)
    {
        var rot = Quaternion.FromToRotation(Vector3.up, axis);
        var line = Prim(PrimitiveType.Capsule, name, grips, _handle, collider: false);
        line.transform.localPosition = pos;
        line.transform.localRotation = rot;
        line.transform.localScale = new Vector3(0.014f, length / 2f, 0.014f);

        var zone = new GameObject(name + " (Greifzone)");
        zone.transform.SetParent(grips, false);
        zone.transform.localPosition = pos;
        zone.transform.localRotation = rot;
        var capsule = zone.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = 0.03f;
        capsule.height = length + 0.06f;
        capsule.isTrigger = true; // wie beim Building-Block-Würfel: nur zum Greifen, schiebt nichts weg
    }

    static void Box(Transform parent, string name, Material m, Vector3 pos, Vector3 size)
    {
        var go = Prim(PrimitiveType.Cube, name, parent, m);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
    }

    static void Line(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var go = Prim(PrimitiveType.Cube, name, parent, _edge, collider: false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
    }

    static void BoxZone(string name, Transform parent, Vector3 pos, Vector3 size, bool water)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var box = go.AddComponent<BoxCollider>();
        box.size = size;
        box.isTrigger = true;
        go.AddComponent<ContentZone>().water = water;
    }

    // ---------- Szene einrichten ----------

    /// Flächenerkennung über die Tiefensensoren (Meta MR Utility Kit) und die nötige Berechtigung.
    static void SetUpSurfaceDetection(Transform root)
    {
        if (Object.FindAnyObjectByType<EnvironmentRaycastManager>(FindObjectsInactive.Include) == null)
        {
            var go = new GameObject("EnvironmentRaycastManager");
            go.transform.SetParent(root, false);
            go.AddComponent<EnvironmentRaycastManager>();
        }

        var manager = Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("Grey-Box: Kein OVRManager gefunden, Raum-Berechtigung bitte von Hand aktivieren.");
            return;
        }
        var so = new SerializedObject(manager);
        so.FindProperty("requestScenePermissionOnStartup").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Bausteine ----------

    /// Statisches Primitiv. Zylinder und Kugeln bekommen einen konvexen MeshCollider,
    /// damit die Kollision auch bei ungleichmäßiger Skalierung zur Form passt.
    static GameObject Prim(PrimitiveType type, string name, Transform parent, Material m, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        var c = go.GetComponent<Collider>();
        if (!collider)
            Object.DestroyImmediate(c);
        else if (type == PrimitiveType.Cylinder || type == PrimitiveType.Sphere)
        {
            Object.DestroyImmediate(c);
            go.AddComponent<MeshCollider>().convex = true;
        }
        return go;
    }

    static GameObject CloneTemplate(Transform parent, string name)
    {
        var go = Object.Instantiate(_template, parent);
        go.name = name;
        go.SetActive(true);
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null && mb.GetType().Name == "BuildingBlock") Object.DestroyImmediate(mb);
        foreach (Transform child in go.transform) child.name = "HandGrab";
        return go;
    }

    /// Greifbares Objekt: Kopie des Building-Block-Würfels mit anderer Form, Größe und Physik.
    static GameObject Grab(Transform parent, Transform home, string name, PrimitiveType type,
        Vector3 scale, Vector3 worldPos, Quaternion rot, Material m, GreyBoxItem.Kind kind, bool physics = true)
    {
        var go = CloneTemplate(parent, name);
        go.transform.position = worldPos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;

        var tmp = GameObject.CreatePrimitive(type);
        go.GetComponent<MeshFilter>().sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;

        foreach (var c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        switch (type)
        {
            case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
            case PrimitiveType.Cylinder:
            case PrimitiveType.Sphere: go.AddComponent<MeshCollider>().convex = true; break;
            default: go.AddComponent<BoxCollider>(); break;
        }

        SetUpItem(go, home, kind, physics);
        return go;
    }

    static void SetUpItem(GameObject go, Transform home, GreyBoxItem.Kind kind, bool physics)
    {
        var rb = go.GetComponent<Rigidbody>();
        rb.isKinematic = !physics;
        rb.useGravity = physics;
        rb.mass = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var grabbable = go.GetComponent<Grabbable>();
        grabbable.InjectOptionalKinematicWhileSelected(true);
        grabbable.InjectOptionalThrowWhenUnselected(physics);

        var item = go.AddComponent<GreyBoxItem>();
        item.kind = kind;
        item.home = home;
    }

    /// Setzt ein Modell als Kind "Modell" ein, mittig und auf die gewünschte Größe skaliert.
    /// AlongY: längste Achse wird lokal Y (Angel). Height: Höhe = size (Pflanzen). Longest: längste Seite = size.
    /// Gibt die Begrenzung im lokalen Raum des Elternobjekts zurück.
    static Bounds FitModel(Transform parent, GameObject model, float size, Fit fit)
    {
        var inst = PrefabUtility.InstantiatePrefab(model, parent) as GameObject;
        if (inst == null) inst = Object.Instantiate(model, parent);
        inst.name = "Modell";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var b = LocalBounds(inst, parent);
        if (fit == Fit.AlongY)
        {
            if (b.size.x >= b.size.y && b.size.x >= b.size.z) inst.transform.localRotation = Quaternion.Euler(0, 0, 90);
            else if (b.size.z >= b.size.y && b.size.z > b.size.x) inst.transform.localRotation = Quaternion.Euler(90, 0, 0);
            b = LocalBounds(inst, parent);
        }

        var current = fit == Fit.Longest ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : b.size.y;
        inst.transform.localScale = Vector3.one * (size / current);
        b = LocalBounds(inst, parent);
        inst.transform.localPosition -= b.center;
        return LocalBounds(inst, parent);
    }

    static Bounds LocalBounds(GameObject go, Transform space)
    {
        var toSpace = space.worldToLocalMatrix;
        var bounds = new Bounds();
        var any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            Bounds local;
            if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null) local = skinned.sharedMesh.bounds;
            else if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
            else continue;

            var m = toSpace * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }

    static GameObject FindTemplate()
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == TemplateName && go.scene.IsValid())
                return go;
        return null;
    }

    /// Material anlegen oder aktualisieren (URP Lit), damit Farbänderungen im Code immer ankommen.
    static Material Mat(string name, Color color, bool transparent = false, float smoothness = 0.5f)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/GreyBox", "Materials");

        var path = $"{MaterialFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Surface", transparent ? 1 : 0);
        m.SetFloat("_Blend", 0);
        m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        m.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
        m.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
        m.SetFloat("_ZWrite", transparent ? 0 : 1);
        if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;

        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }
}
