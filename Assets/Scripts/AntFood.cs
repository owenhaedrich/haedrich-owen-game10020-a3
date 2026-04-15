using UnityEngine;

public class AntFood : MonoBehaviour, ISmellable
{
    public int foodAmount = 5;
    private int initialFoodAmount;
    private Vector3 initialScale;

    private void Start()
    {
        initialFoodAmount = foodAmount;
        initialScale = transform.localScale;
    }

    public void TakeFood()
    {
        foodAmount--;
        
        if (foodAmount <= 0)
        {
            gameObject.SetActive(false);
        }
        else
        {
            UpdateScale();
        }
    }

    private void UpdateScale()
    {
        float ratio = (float)foodAmount / initialFoodAmount;
        transform.localScale = initialScale * ratio;
    }

    public ScentType GetScentType()
    {
        return ScentType.Food;
    }
}
