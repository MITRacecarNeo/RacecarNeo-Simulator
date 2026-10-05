using UnityEditor;

/// <summary>
/// Import settings for the RACECAR Neo V2 car model. The FBX is authored at the world scale
/// (1 unit = 1 dm) with the car root at the tire contact plane, facing +Z; these settings keep
/// that geometry unchanged and skip everything the car does not use.
/// </summary>
public class RacecarModelImport : AssetPostprocessor
{
    /// <summary>
    /// Folder holding the car model.
    /// </summary>
    public const string ModelFolder = "Assets/Models/RacecarNeoV2/";

    /// <summary>
    /// Path of the car model.
    /// </summary>
    public const string ModelPath = RacecarModelImport.ModelFolder + "RacecarNeoV2.fbx";

    private void OnPreprocessModel()
    {
        if (!this.assetPath.StartsWith(RacecarModelImport.ModelFolder))
        {
            return;
        }

        ModelImporter importer = (ModelImporter)this.assetImporter;
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.addCollider = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.generateSecondaryUV = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }
}
