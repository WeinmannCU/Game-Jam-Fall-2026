using UnityEngine;
using UnityEngine.UI;

public sealed class ShipDamageUI : MonoBehaviour
{
    private const float SinkingFraction = 0.75f;
    private const float FillInset = 4f;

    [SerializeField] private ShipDamageTracker damageTracker;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color greenColor = new Color(0.18f, 0.8f, 0.3f);
    [SerializeField] private Color yellowColor = new Color(1f, 0.82f, 0.12f);
    [SerializeField] private Color redColor = new Color(0.9f, 0.12f, 0.1f);
    [SerializeField] private Color criticalColor = new Color(0.28f, 0.015f, 0.025f);

    private Sprite generatedFillSprite;

    private void OnEnable()
    {
        if (damageTracker == null || fillImage == null)
        {
            Debug.LogError("ShipDamageUI needs a ShipDamageTracker and fill Image.", this);
            enabled = false;
            return;
        }

        if (fillImage.sprite == null)
        {
            generatedFillSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
            fillImage.sprite = generatedFillSprite;
        }

        damageTracker.DamageChanged += Refresh;
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (damageTracker != null)
        {
            damageTracker.DamageChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        if (generatedFillSprite != null)
        {
            Destroy(generatedFillSprite);
        }
    }

    private void Refresh()
    {
        float damage = damageTracker.DamageFraction;
        float fillAmount = Mathf.Clamp01(damage / SinkingFraction);
        fillImage.type = Image.Type.Simple;

        RectTransform fillRect = fillImage.rectTransform;
        RectTransform frameRect = fillRect.parent as RectTransform;
        if (frameRect == null)
        {
            Debug.LogError("ShipDamageUI fill Image must be a child of a RectTransform bar frame.", this);
            return;
        }

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = new Vector2(FillInset, 0f);
        fillRect.sizeDelta = new Vector2(
            Mathf.Max(0f, frameRect.rect.width - FillInset * 2f) * fillAmount,
            -FillInset * 2f);

        if (damageTracker.IsSinking || damageTracker.IsFinalTileWarning)
        {
            fillImage.color = criticalColor;
        }
        else if (damage >= 0.65f)
        {
            fillImage.color = redColor;
        }
        else if (damage >= 0.25f)
        {
            fillImage.color = yellowColor;
        }
        else
        {
            fillImage.color = greenColor;
        }
    }
}
