using UnityEngine;
using System;

public enum GameMode
{
    Exploration,
    Battle
}

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public GameMode CurrentMode { get; private set; } = GameMode.Exploration;

    public event Action<GameMode> OnModeChanged;

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

    public void SetMode(GameMode newMode)
    {
        if (CurrentMode == newMode) return;

        CurrentMode = newMode;
        OnModeChanged?.Invoke(newMode);
    }

    public bool IsExploration => CurrentMode == GameMode.Exploration;
    public bool IsBattle => CurrentMode == GameMode.Battle;
}
