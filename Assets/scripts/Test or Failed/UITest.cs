using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITest : MonoBehaviour
{
    [SerializeField] private Button button1 = default;
    //[SerializeField] private Button button2 = default;
    //[SerializeField] private Button button3 = default;

    [SerializeField] private TextMeshProUGUI debugText = default;

    void Start()
    {
        button1.onClick.AddListener(() => debugText.text = $"pushed button is {button1.name}");
        //button2.onClick.AddListener(() => debugText.text = $"pushed button is {button2.name}");
        //button3.onClick.AddListener(() => debugText.text = $"pushed button is {button3.name}");
    }
}