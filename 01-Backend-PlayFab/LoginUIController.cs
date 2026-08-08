using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using PlayFab;
using PlayFab.ClientModels;

/// <summary>
/// Login screen. Email/password -> PlayFab (auto-registers new users) -> MainMenu.
///
/// PLACEHOLDER BEHAVIOUR (professional UX):
///  - Empty field shows centered "Enter Email" / "Enter Password" + icon.
///  - As soon as the user types, the placeholder + icon hide and typing starts from the LEFT.
///  - Clearing the field brings the placeholder + icon back.
///
/// PUT THIS ON: the same object (with the UIDocument assigned).
/// Set "Main Menu Scene Name" in the Inspector and add it to Build Settings.
/// </summary>
public class LoginUIController : MonoBehaviour
{
    public UIDocument uiDocument;

    [Header("Scene to load after login")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Placeholder text")]
    [SerializeField] private string emailPlaceholder = "Enter Email";
    [SerializeField] private string passwordPlaceholder = "Enter Password";

    private TextField emailField;
    private TextField passwordField;
    private VisualElement emailIcon;
    private VisualElement passwordIcon;
    private Label errorText;
    private Button signInButton;

    // real values typed by the user (placeholder is not a real value)
    private string realEmail = "";
    private string realPassword = "";
    private bool emailHasText = false;
    private bool passwordHasText = false;
    private bool isBusy;

    void Start()
    {
        var root = uiDocument.rootVisualElement;
        if (AudioManager.I != null) AudioManager.I.PlayLoginMusic();
        emailField = root.Q<TextField>("email");
        passwordField = root.Q<TextField>("password");
        emailIcon = root.Q<VisualElement>("envelope");
        passwordIcon = root.Q<VisualElement>("Lock");
        errorText = root.Q<Label>("errortext");
        signInButton = root.Q<Button>("SignIn");

        SetupField(emailField, true);
        SetupField(passwordField, false);

        if (signInButton != null) signInButton.clicked += CheckLogin;
        

        HideError();
    }

    // ========================= PLACEHOLDER SETUP =========================
    void SetupField(TextField field, bool isEmail)
    {
        if (field == null) return;

        // start in placeholder state (centered text, icon visible)
        ShowPlaceholder(field, isEmail);

        // When the field gains focus: if it's still showing placeholder, clear it.
        field.RegisterCallback<FocusInEvent>(evt =>
        {
            bool hasText = isEmail ? emailHasText : passwordHasText;
            if (!hasText)
            {
                field.SetValueWithoutNotify("");
                ApplyTypingStyle(field, isEmail);   // left align, hide icon
            }
        });

        // When the field loses focus: if empty, restore placeholder.
        field.RegisterCallback<FocusOutEvent>(evt =>
        {
            if (string.IsNullOrEmpty(field.value))
            {
                if (isEmail) { realEmail = ""; emailHasText = false; }
                else { realPassword = ""; passwordHasText = false; }
                ShowPlaceholder(field, isEmail);
            }
        });

        // While typing: store real value, keep typing style, hide icon.
        field.RegisterValueChangedCallback(evt =>
        {
            string v = evt.newValue;

            // ignore the programmatic placeholder text
            string placeholder = isEmail ? emailPlaceholder : passwordPlaceholder;
            if (v == placeholder) return;

            bool hasText = !string.IsNullOrEmpty(v);
            if (isEmail) { realEmail = v; emailHasText = hasText; }
            else { realPassword = v; passwordHasText = hasText; }

            if (hasText) ApplyTypingStyle(field, isEmail);
            else ShowPlaceholderKeepFocus(field, isEmail);

            HideError();
        });
    }

    // placeholder visible: icon LEFT + text CENTER (both shown), password mask OFF
    void ShowPlaceholder(TextField field, bool isEmail)
    {
        string placeholder = isEmail ? emailPlaceholder : passwordPlaceholder;
        field.isPasswordField = false;
        field.SetValueWithoutNotify(placeholder);
        field.style.unityTextAlign = TextAnchor.MiddleCenter;

        // icon shown on the left at start
        var icon = isEmail ? emailIcon : passwordIcon;
        if (icon != null) icon.style.display = DisplayStyle.Flex;

        // dim the placeholder text a bit
        var input = field.Q(className: "unity-base-field__input");
        if (input != null) input.style.color = new Color(1f, 1f, 1f, 0.55f);
    }

    // used while the field is focused and the user deletes everything
    void ShowPlaceholderKeepFocus(TextField field, bool isEmail)
    {
        var icon = isEmail ? emailIcon : passwordIcon;
        if (icon != null) icon.style.display = DisplayStyle.None;
        field.style.unityTextAlign = TextAnchor.MiddleLeft;
        var input = field.Q(className: "unity-base-field__input");
        if (input != null) input.style.color = Color.white;
    }

    // typing: left aligned, full white, icon hidden, password mask ON for password
    void ApplyTypingStyle(TextField field, bool isEmail)
    {
        field.style.unityTextAlign = TextAnchor.MiddleLeft;

        var icon = isEmail ? emailIcon : passwordIcon;
        if (icon != null) icon.style.display = DisplayStyle.None;

        if (!isEmail) field.isPasswordField = true;   // mask the password

        var input = field.Q(className: "unity-base-field__input");
        if (input != null) input.style.color = Color.white;
    }

    // ========================= LOGIN =========================
    void CheckLogin()
    {
        if (isBusy) return;

        string email = realEmail;
        string password = realPassword;

        if (!IsValidEmail(email))
        {
            ShowError("Enter a valid email (e.g. name@gmail.com).");
            return;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        HideError();
        SetBusy(true, "Signing in...");
        LoginWithPlayFab(email, password);
    }

    bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return false;

        // 1) Basic format: something@something.something
        string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
        if (!Regex.IsMatch(email, pattern)) return false;

        // 2) Extract the domain (after @)
        string domain = email.Substring(email.IndexOf('@') + 1).ToLower();

        // 3) Reject common typo domains
        string[] typoDomains = {
            "gmial.com", "gmai.com", "gmal.com", "gmail.co", "gmail.con", "gmaill.com",
            "gnail.com", "gmail.cm", "gmil.com", "gmailc.com", "gemail.com",
            "yahooo.com", "yaho.com", "yahoo.co", "yhaoo.com", "yahool.com",
            "hotmial.com", "hotmil.com", "hotmai.com", "hotmail.co", "hotnail.com",
            "outlok.com", "outlook.co", "outloo.com", "outlokk.com",
            "iclod.com", "icloud.co", "iclould.com"
        };
        foreach (var t in typoDomains)
            if (domain == t) return false;

        // 4) Allow only well-known providers (international standard)
        string[] validDomains = {
            "gmail.com", "yahoo.com", "outlook.com", "hotmail.com", "live.com",
            "icloud.com", "protonmail.com", "proton.me", "aol.com", "msn.com",
            "yandex.com", "zoho.com", "gmx.com", "mail.com", "edu.pk", "yahoo.co.uk"
        };
        foreach (var v in validDomains)
            if (domain == v) return true;

        // Domain not in the allowed list -> reject
        return false;
    }

    // ========================= BUSY / BUTTON STATE =========================
    void SetBusy(bool busy, string label)
    {
        isBusy = busy;
        if (signInButton != null)
        {
            signInButton.text = busy ? label : "Sign In";
            signInButton.SetEnabled(!busy);
        }
    }

   
    void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.style.display = DisplayStyle.Flex;
    }

    void HideError()
    {
        if (errorText == null) return;
        errorText.text = "";
        errorText.style.display = DisplayStyle.None;
    }


    void LoginWithPlayFab(string email, string password)
    {
        var request = new LoginWithEmailAddressRequest { Email = email, Password = password };

        PlayFabClientAPI.LoginWithEmailAddress(request,
           result =>
           {
               Debug.Log("Login Success");
               HideError();
               PlayerPrefs.SetString("UserEmail", email);
               PlayerPrefs.Save();
               GoToMainMenu();
           },
            error =>
            {
                if (error.Error == PlayFabErrorCode.AccountNotFound)
                {
                    Debug.Log("Account not found -> registering...");
                    RegisterWithPlayFab(email, password);
                }
                else
                {
                    SetBusy(false, "Sign In");
                    ShowError(FriendlyError(error));
                    Debug.Log(error.GenerateErrorReport());
                }
            });
    }

    void RegisterWithPlayFab(string email, string password)
    {
        string username = email.Split('@')[0];
        if (username.Length > 20) username = username.Substring(0, 20);

        var request = new RegisterPlayFabUserRequest
        {
            Email = email,
            Password = password,
            Username = username,
            RequireBothUsernameAndEmail = false
        };

        PlayFabClientAPI.RegisterPlayFabUser(request,
          result =>
          {
              Debug.Log("Register Success -> entering game");
              HideError();
              PlayerPrefs.SetString("UserEmail", email);
              PlayerPrefs.Save();
              GoToMainMenu();
          },
            error =>
            {
                SetBusy(false, "Sign In");
                ShowError(FriendlyError(error));
                Debug.Log(error.GenerateErrorReport());
            });
    }

    string FriendlyError(PlayFabError error)
    {
        switch (error.Error)
        {
            case PlayFabErrorCode.InvalidEmailOrPassword:
            case PlayFabErrorCode.InvalidUsernameOrPassword:
            case PlayFabErrorCode.AccountNotFound:
                return "Incorrect email or password.";
            case PlayFabErrorCode.InvalidParams:
            case PlayFabErrorCode.InvalidEmailAddress:
                return "Please enter a valid email address.";
            case PlayFabErrorCode.InvalidPassword:
                return "Incorrect password. Please try again.";
            case PlayFabErrorCode.EmailAddressNotAvailable:
                return "This email is already registered.";
            case PlayFabErrorCode.ServiceUnavailable:
            case PlayFabErrorCode.InternalServerError:
                return "Server unavailable. Please try again later.";
            default:
                return "Connection error. Check your internet and try again.";
        }
    }

    void GoToMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("Main Menu Scene Name is empty in the Inspector.");
            return;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }
    void PlayClickSound()
    {
        if (AudioManager.I != null) AudioManager.I.PlayClick();
    }
}