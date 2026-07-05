using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] GameObject      mainPanel;
    [SerializeField] GameObject      settingsPanel;
    [SerializeField] Slider          sensitivitySlider;
    [SerializeField] TextMeshProUGUI sensitivityLabel;
    [SerializeField] Toggle          fullscreenToggle;

    void Start()
    {
        sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 2f);
        if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;
        UpdateSensLabel();
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void StartGame()    => SceneManager.LoadScene("OutdoorsScene");
    public void ExitGame()     => Application.Quit();

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void OnSensitivityChanged(float v)
    {
        PlayerPrefs.SetFloat("Sensitivity", v);
        UpdateSensLabel();
    }

    public void OnFullscreenChanged(bool v) => Screen.fullScreen = v;

    void UpdateSensLabel()
    {
        if (sensitivityLabel)
            sensitivityLabel.text = $"Hassasiyet: {sensitivitySlider.value:0.0}";
    }
}
