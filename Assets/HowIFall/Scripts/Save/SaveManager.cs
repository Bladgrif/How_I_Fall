using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SaveFileName = "save_01.json";

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Save()
    {
        GameState gameState = GameState.Instance;

        if (gameState == null)
        {
            Debug.LogWarning("SaveManager: GameState.Instance is missing. Save skipped.");
            return;
        }

        SaveData saveData = new SaveData
        {
            currentSceneId = gameState.currentSceneId,
            currentLineIndex = gameState.currentLineIndex,
            lust = gameState.lust,
            romance = gameState.romance,
            purity = gameState.purity,
            corruptionLevel = gameState.corruptionLevel,
            selfControl = gameState.selfControl,
            suspicion = gameState.suspicion,
            trustMasha = gameState.trustMasha,
            trustArtem = gameState.trustArtem,
            leraInterest = gameState.leraInterest
        };

        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"SaveManager: game saved to '{SavePath}'.");
    }

    public bool Load()
    {
        if (!HasSave())
        {
            return false;
        }

        string json = File.ReadAllText(SavePath);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

        if (saveData == null)
        {
            return false;
        }

        GameState gameState = GameState.EnsureInstance();
        gameState.currentSceneId = saveData.currentSceneId;
        gameState.currentLineIndex = saveData.currentLineIndex;
        gameState.lust = saveData.lust;
        gameState.romance = saveData.romance;
        gameState.purity = saveData.purity;
        gameState.corruptionLevel = saveData.corruptionLevel;
        gameState.selfControl = saveData.selfControl;
        gameState.suspicion = saveData.suspicion;
        gameState.trustMasha = saveData.trustMasha;
        gameState.trustArtem = saveData.trustArtem;
        gameState.leraInterest = saveData.leraInterest;
        gameState.hasLoadedSave = true;
        return true;
    }

    public bool HasSave()
    {
        return File.Exists(SavePath);
    }

    public string GetSavePathForDebug()
    {
        return SavePath;
    }

    public void DeleteSave()
    {
        if (!HasSave())
        {
            return;
        }

        File.Delete(SavePath);
    }
}
