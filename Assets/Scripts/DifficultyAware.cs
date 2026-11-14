using UnityEngine;

public interface IDifficultyAware
{
    // 1 = starting difficulty, rises each time the manager ramps up the speed.
    void SetDifficulty(float difficulty);
}