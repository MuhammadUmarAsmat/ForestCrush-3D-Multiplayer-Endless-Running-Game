using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class CharacterSelector : MonoBehaviour
{
    [Header("Characters (order matters)")]
    [SerializeField] private GameObject[] characters;   // 4 characters (CharacterHolder ke andar wale)

    [Header("Character Names (same order)")]
    [SerializeField] private string[] characterNames = { "Male1", "Female1", "Male2", "Female2" };

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameLabel;  // selected character ka naam dikhane ke liye
    [SerializeField] private TextMeshProUGUI selectButtonLabel;
    [Header("Rotation")]
    [SerializeField] private float spinSpeed = 40f;      // preview ghoomne ki speed

    [Header("Scene to load on SELECT")]
    [SerializeField] private string sceneToLoad = "MainMenuScene"; // select ke baad kahan jaye

    private int currentIndex = 0;

    private void Start()
    {
        // Pehle se saved selection load karo (agar ho)
        currentIndex = PlayerPrefs.GetInt(CharacterPrefs.Key, 0);
        if (currentIndex < 0 || currentIndex >= characters.Length) currentIndex = 0;
        ShowCharacter(currentIndex);
        if (AudioManager.I != null) AudioManager.I.PlayCharacterMusic();
        string mode = PlayerPrefs.GetString("GameMode", "single");
        if (selectButtonLabel != null)
            selectButtonLabel.text = (mode == "viewonly") ? "BACK" : "SELECT";
    }

    private void Update()
    {
        // Selected character ko ghumao (preview spin)
        if (characters != null && currentIndex < characters.Length && characters[currentIndex] != null)
        {
            characters[currentIndex].transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
        }
    }

    // RIGHT arrow button
    public void NextCharacter()
    {
        currentIndex++;
        if (currentIndex >= characters.Length) currentIndex = 0;
        ShowCharacter(currentIndex);
    }

    // LEFT arrow button
    public void PreviousCharacter()
    {
        currentIndex--;
        if (currentIndex < 0) currentIndex = characters.Length - 1;
        ShowCharacter(currentIndex);
    }

    private void ShowCharacter(int index)
    {
        // Sirf selected character ON, baaki OFF
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
            {
                characters[i].SetActive(i == index);
                characters[i].transform.rotation = Quaternion.identity; // reset rotation
            }
        }

        // Naam update karo
        if (nameLabel != null && index < characterNames.Length)
            nameLabel.text = characterNames[index];
    }

    // SELECT/CONFIRM button
    public void ConfirmSelection()
    {
        // Save the chosen character (so MainMenu / game can use it later)
        PlayerPrefs.SetInt(CharacterPrefs.Key, currentIndex);
        if (currentIndex < characterNames.Length)
            PlayerPrefs.SetString("SelectedCharacterName", characterNames[currentIndex]);

        string gender = (currentIndex % 2 == 0) ? "male" : "female";
        PlayerPrefs.SetString("SelectedGender", gender);
        PlayerPrefs.Save();

        // Route based on game mode
        string mode = PlayerPrefs.GetString("GameMode", "single");

        if (mode == "viewonly")
            SceneManager.LoadScene("MainMenuScene");      // view only -> back to menu
        else if (mode == "multi")
            SceneManager.LoadScene("MultiplayerScene");   // multiplayer
        else
            SceneManager.LoadScene("SinglePlayerScene");  // single
    }
}
