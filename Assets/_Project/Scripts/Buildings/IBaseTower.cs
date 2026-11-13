using UnityEngine;

public interface IBaseTower
{
    void ApplyUpgrade(int level);
    int GetSellRefund();
    int GetMaxLevel();
    TowerUpgrade GetCurrentUpgrade();
    TowerUpgrade GetNextUpgrade();
    void SetPlot(Plot plot);
    float GetRange();
    int GetCurrentLevel();
    float GetCreationTime();
}