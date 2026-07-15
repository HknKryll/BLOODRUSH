using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    float current;

    public UnityEvent onDeath;
    public UnityEvent<float> onHealthChanged; // 0-1 normalized

    public float Current => current;
    public float Max => maxHealth;
    public bool IsAlive => current > 0f;

    void Awake() => current = maxHealth;

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;

        current = Mathf.Max(0f, current - amount);
        onHealthChanged.Invoke(current / maxHealth);

        if (current <= 0f)
            onDeath.Invoke();
    }

    public void Heal(float amount)
    {
        current = Mathf.Min(maxHealth, current + amount);
        onHealthChanged.Invoke(current / maxHealth);
    }

    // Checkpoint respawn — tam canla geri dön
    public void Revive()
    {
        current = maxHealth;
        onHealthChanged.Invoke(1f);
    }
}
