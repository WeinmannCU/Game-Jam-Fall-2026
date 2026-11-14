using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SurvivalTimer : MonoBehaviour
{
    [SerializeField] private Text timerLabel;
    [SerializeField] private ShipDamageTracker gameState;

    private double elapsedSeconds;
    private long displayedMilliseconds = -1;
    private bool isRunning;

    public double ElapsedSeconds => elapsedSeconds;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (timerLabel == null)
        {
            Debug.LogError("SurvivalTimer needs a timer Text reference.", this);
            enabled = false;
            return;
        }

        ResetTimer();
    }

    private void OnEnable()
    {
        if (gameState != null)
        {
            gameState.GameOver += Stop;
        }
    }

    private void OnDisable()
    {
        if (gameState != null)
        {
            gameState.GameOver -= Stop;
        }
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        elapsedSeconds += Time.deltaTime;
        RefreshLabel();
    }

    /// <summary>Stops the survival timer, for example when the ship sinks.</summary>
    public void Stop()
    {
        isRunning = false;
    }

    /// <summary>Resets the elapsed time and starts a new survival run.</summary>
    public void ResetTimer()
    {
        elapsedSeconds = 0d;
        isRunning = true;
        displayedMilliseconds = -1;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        long totalMilliseconds = (long)Math.Floor(elapsedSeconds * 1000d);
        if (displayedMilliseconds == totalMilliseconds)
        {
            return;
        }

        displayedMilliseconds = totalMilliseconds;
        long totalSeconds = totalMilliseconds / 1000;
        long minutes = totalSeconds / 60;
        int seconds = (int)(totalSeconds % 60);
        int milliseconds = (int)(totalMilliseconds % 1000);
        timerLabel.text = $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }
}
