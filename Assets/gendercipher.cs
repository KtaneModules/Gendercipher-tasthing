using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class gendercipher : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] buttons;
    public KMSelectable submitButton;
    public Renderer[] buttonRenders;
    public Renderer[] buttonSymbols;
    public Renderer[] screens;
    public Texture[] symbols;
    public TextMesh[] colorblindTexts;
    public Color[] buttonColors;

    public Color[] pride;
    public Color[] bisexual;
    public Color[] pansexual;
    public Color[] transgender;
    public Color[] genderqueer;
    public Color[] nonbinary;
    public Color[] asexual;
    public Color[] aromantic;
    private Color[][] allFlagPatterns;
    public Color darkGray;

    private int column;
    private int[] displayedSymbols;
    private int[] rotations;
    private int[] displayedRotations;
    private int[] originalRotations;
    private string word;
    private bool encryptionDirectionClockwise;
    private int[] displayedFlags;
    private int solutionWord;
    private int[] solution = new int[4];

    private static readonly string[] genderNames = new string[16] { "female", "male", "bigender", "androgyne", "neutrois", "agender", "intergender", "demiboy", "demigirl", "third gender", "non-binary", "poligender", "genderfluid", "transgender", "tranvesti n-b", "aliagender" };
    private static readonly string[] flagNames = new string[8] { "pride", "bisexual", "pansexual", "transgender", "genderqueer", "non-binary", "asexual", "aromantic" };
    private static readonly string[] letterTable = new string[16] { "AIRMP", "XKCAE", "DCOPK", "BEBIA", "GDQRM", "EFDUN", "JALTI", "HLXOB", "MOHWH", "KZUER", "PUWYU", "NWJBT", "SVVDO", "QXIGV", "VPAJW", "TRZLQ" };
    private static readonly string[] wordList = new string[64] { "HEAT", "GOLD", "STAR", "BOYS", "FAKE", "BANK", "TRIP", "SWIM", "SIDE", "SOLO", "HERO", "GASP", "FLAT", "MOLD", "BANG", "COAT", "LANE", "URGE", "BOOM", "TUNE", "FATE", "LACK", "JOKE", "UNIT", "RATE", "TALK", "PUMP", "BOOT", "HURT", "BOND", "SLAP", "OVEN", "JURY", "GAIN", "ROCK", "BLUE", "FARE", "POLE", "GOOD", "MENU", "WARM", "PAST", "LEAF", "SLOW", "LOUD", "PLOT", "FILE", "RANK", "PURE", "WRAP", "TALK", "USER", "BOLT", "BARK", "JUMP", "QUIT", "PILE", "RACK", "ROOT", "BOLD", "AXIS", "TENT", "LICK", "VIEW" };
    private static readonly string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private int[] storedTimes = new int[4];
    private bool[] longPresses = new bool[4];
    private Coroutine[] countingCoroutines = new Coroutine[4];
    private Coroutine[] movementCoroutines = new Coroutine[4];
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in buttons)
        {
            button.OnInteract += delegate { PressButton(button); return false; };
            button.OnInteractEnded += delegate { ReleaseButton(button); };
        }
        submitButton.OnInteract += delegate () { Submit(); return false; };
        allFlagPatterns = new Color[][] { pride, bisexual, pansexual, transgender, genderqueer, nonbinary, asexual, aromantic };
        foreach (GameObject t in colorblindTexts.Select(x => x.gameObject))
            t.SetActive(GetComponent<KMColorblindMode>().ColorblindModeActive);
    }

    private void Start()
    {
        if (bomb.GetBatteryCount() == bomb.GetIndicators().Count())
            column = 4;
        else if (bomb.GetBatteryCount() == bomb.GetBatteryHolderCount())
            column = 3;
        else if (bomb.GetOnIndicators().Count() == bomb.GetOffIndicators().Count())
            column = 2;
        else if (bomb.GetSerialNumberLetters().Count() == bomb.GetSerialNumberNumbers().Count())
            column = 1;
        else
            column = 0;
        Debug.LogFormat("[Gendercipher #{0}] The starting column is {1}.", moduleId, column + 1);
        var portDirection = bomb.GetSerialNumberNumbers().Last() % 2 == 0 ? -1 : 1;
        for (int i = 0; i < bomb.GetPorts().Distinct().Count(); i++)
            column = (column + 5 + portDirection) % 5;
        Debug.LogFormat("[Gendercipher #{0}] Column after moving: {1}.", moduleId, column + 1);
        var lowModules = bomb.GetModuleNames().Count() <= 23;
        var primeModules = IsPrime(bomb.GetModuleNames().Count());
        encryptionDirectionClockwise = (primeModules && !lowModules) || (!primeModules && lowModules);
        displayedSymbols = new int[4].Select(x => x = rnd.Range(0, 16)).ToArray();
        word = wordList.PickRandom();
        rotations = new int[4];
        displayedRotations = new int[4];
        for (int i = 0; i < 4; i++)
        {
            rotations[i] = alphabet.IndexOf(word[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]) >= 0 ? alphabet.IndexOf(word[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]) : 26 + (alphabet.IndexOf(word[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]));
            displayedRotations[i] = rotations[i] % 8;
            buttonSymbols[i].material.mainTexture = symbols[displayedSymbols[i]];
            buttonSymbols[i].transform.localEulerAngles = new Vector3(90f, (encryptionDirectionClockwise ? 45f : -45f) * displayedRotations[i], 0f);
            buttonRenders[i].material.color = buttonColors[rotations[i] / 8];
            var letters = rotations.Select(x => "WCPG"[x / 8]).ToArray();
            colorblindTexts[0].text = string.Format("{0}{1}\n{2}{3}", letters[0], letters[1], letters[2], letters[3]);
        }
        originalRotations = displayedRotations.ToArray();
        Debug.LogFormat("[Gendercipher #{0}] The displayed gender symbols are: {1}.", moduleId, displayedSymbols.Select(x => genderNames[x]).Join(", "));
        Debug.LogFormat("[Gendercipher #{0}] These correspond to the letters: {1}.", moduleId, displayedSymbols.Select(x => letterTable[x][column]).Join());
        Debug.LogFormat("[Gendercipher #{0}] The symbols have been rotated these amounts of times {1}clockwise: {2}.", moduleId, encryptionDirectionClockwise ? "" : "counter", rotations.Join(", "));
        Debug.LogFormat("[Gendercipher #{0}] Therefore, the decrypted word is {1}.", moduleId, word);

        displayedFlags = new int[2].Select(x => x = rnd.Range(0, 8)).ToArray();
        var displayOrder = bomb.GetSerialNumberLetters().Any(x => "AEIOU".Contains(x)) ? new int[] { 0, 1 } : new int[] { 1, 0 };
        Debug.LogFormat("[Gendercipher #{0}] Apply the effects of the {1} display first.", moduleId, displayOrder[0] == 0 ? "top" : "side");
        Debug.LogFormat("[Gendercipher #{0}] Displayed flag patterns: {1}.", moduleId, displayedFlags.Select(x => flagNames[x]).Join(", "));

        solutionWord = Array.IndexOf(wordList, word);
        for (int i = 0; i < 2; i++)
        {
            switch (displayedFlags[displayOrder[i]])
            {
                case 0:
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().Max(); j++)
                        solutionWord = solutionWord % 8 == 0 ? solutionWord + 7 : solutionWord - 1;
                    break;
                case 1:
                    for (int j = 0; j < 9 - bomb.GetSerialNumberNumbers().Min(); j++)
                        solutionWord = solutionWord % 8 == 7 ? solutionWord - 7 : solutionWord + 1;
                    break;
                case 2:
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().Sum(); j++)
                        solutionWord = solutionWord / 8 == 0 ? solutionWord + 56 : solutionWord - 8;
                    break;
                case 3:
                    for (int j = 0; j < bomb.GetSerialNumberNumbers().Last(); j++)
                        solutionWord = solutionWord / 8 == 7 ? solutionWord - 56 : solutionWord + 8;
                    break;
                case 4:
                    var x1 = solutionWord % 8;
                    var y1 = solutionWord / 8;
                    x1 = (x1 + 4) % 8;
                    solutionWord = (x1 % 8) + 8 * (y1 % 8);
                    break;
                case 5:
                    solutionWord = (solutionWord + 32) % 64;
                    break;
                case 6:
                    solutionWord = (solutionWord + 32 % 64);
                    var x3 = solutionWord % 8;
                    var y3 = solutionWord / 8;
                    x3 = (x3 + 4) % 8;
                    solutionWord = (x3 % 8) + 8 * (y3 % 8);
                    break;
                case 7:
                    for (int j = 0; j < Math.Pow(bomb.GetPortCount(), 2); j++)
                        solutionWord = (solutionWord + 1) % 64;
                    break;
            }
        }
        var newWord = wordList[solutionWord];
        Debug.LogFormat("[Gendercipher #{0}] The word reached after transpositions is {1}.", moduleId, newWord);

        var newRotations = new int[4];
        for (int i = 0; i < 4; i++)
            newRotations[i] = alphabet.IndexOf(newWord[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]) >= 0 ? alphabet.IndexOf(newWord[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]) : 26 + (alphabet.IndexOf(newWord[i]) - alphabet.IndexOf(letterTable[displayedSymbols[i]][column]));
        solution = newRotations.Select(x => x % 8).ToArray();
        if (!encryptionDirectionClockwise)
            solution = solution.Select(x => x = (8 - x) % 8).ToArray();
        Debug.LogFormat("[Gendercipher #{0}] Set each symbol to these rotations in order: {1}.", moduleId, solution.Join(", "));

        for (int i = 0; i < 2; i++)
            StartCoroutine(CycleDisplay(screens[i], i, colorblindTexts[i + 1]));
    }

    private void PressButton(KMSelectable button)
    {
        button.AddInteractionPunch(.25f);
        audio.PlaySoundAtTransform("press", button.transform);
        if (moduleSolved)
            return;
        var ix = Array.IndexOf(buttons, button);
        storedTimes[ix] = (int)bomb.GetTime();
        if (countingCoroutines[ix] != null)
        {
            StopCoroutine(countingCoroutines[ix]);
            countingCoroutines[ix] = null;
            longPresses[ix] = false;
        }
        countingCoroutines[ix] = StartCoroutine(CountUp(ix));
    }

    private void ReleaseButton(KMSelectable button)
    {
        if (moduleSolved)
            return;
        var ix = Array.IndexOf(buttons, button);
        if (countingCoroutines[ix] != null)
        {
            StopCoroutine(countingCoroutines[ix]);
            countingCoroutines[ix] = null;
        }
        if (!longPresses[ix])
            SetDirection((displayedRotations[ix] + 9) % 8, ix);
        else
        {
            longPresses[ix] = false;
            SetDirection(originalRotations[ix], ix);
        }
    }

    private void SetDirection(int direction, int ix)
    {
        if (movementCoroutines[ix] != null)
            StopCoroutine(movementCoroutines[ix]);
        if (displayedRotations[ix] != direction)
        {
            displayedRotations[ix] = direction;
            movementCoroutines[ix] = StartCoroutine(MoveSymbol(displayedRotations[ix], ix));
        }
    }

    private void Submit()
    {
        submitButton.AddInteractionPunch(.5f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, submitButton.transform);
        if (moduleSolved)
            return;
        Debug.LogFormat("[Gendercipher #{0}] Submitted rotations: {1}.", moduleId, displayedRotations.Join(", "));
        if (displayedRotations.SequenceEqual(solution))
        {
            module.HandlePass();
            moduleSolved = true;
            audio.PlaySoundAtTransform("solve", transform);
            Debug.LogFormat("[Gendercipher #{0}] That was correct. Module solved!", moduleId);
        }
        else
        {
            module.HandleStrike();
            Debug.LogFormat("[Gendercipher #{0}] That was incorrect. Strike!", moduleId);
        }
    }

    private IEnumerator MoveSymbol(int end, int ix)
    {
        var elapsed = 0f;
        var duration = .25f;
        var startRotation = buttonSymbols[ix].transform.localRotation;
        var endRotation = Quaternion.Euler(90f, (encryptionDirectionClockwise ? 45f : -45f) * end, 0f);
        while (elapsed < duration)
        {
            buttonSymbols[ix].transform.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        buttonSymbols[ix].transform.localRotation = endRotation;
    }

    private IEnumerator CountUp(int ix)
    {
        yield return new WaitForSeconds(1f);
        longPresses[ix] = true;
        audio.PlaySoundAtTransform("deedleweep", buttons[ix].transform);
    }

    private IEnumerator CycleDisplay(Renderer screen, int ix, TextMesh t)
    {
        var pattern = allFlagPatterns[displayedFlags[ix]];
        var letters = new string[] { "ROYGBP", "BPI", "IYC", "CIW", "PWG", "YWPK", "KAWP", "KAWG", }[displayedFlags[ix]];
        var duration = .25f;
    restartSequence:
        foreach (Color c in pattern)
        {
            var elapsed = 0f;
            t.text = letters[Array.IndexOf(pattern, c)].ToString();
            while (elapsed < duration)
            {
                screen.material.color = Color.Lerp(darkGray, c, elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            screen.material.color = c;
            elapsed = 0f;
            yield return new WaitForSeconds(.25f);
            t.text = "";
            while (elapsed < duration)
            {
                screen.material.color = Color.Lerp(c, darkGray, elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }
            screen.material.color = darkGray;
            yield return new WaitForSeconds(.15f);
        }
        if (!moduleSolved)
            goto restartSequence;
    }

    private static bool IsPrime(int number)
    {
        if (number <= 1) return false;
        if (number == 2) return true;
        if (number % 2 == 0) return false;

        var boundary = (int)Math.Floor(Math.Sqrt(number));

        for (int i = 3; i <= boundary; i += 2)
            if (number % i == 0)
                return false;

        return true;
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} <TL/TR/BL/BR> <#> [Rotates the symbol in that position clockwise # times, where # is a number from 1 to 7.] !{0} submit [Presses the submit button.] !{0} reset [Resets every symbol to its original rotation.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim().ToUpperInvariant();
        var inputArray = input.Split(' ').ToArray();
        if (inputArray.Length == 2)
        {
            var directions = new string[] { "TL", "TR", "BL", "BR" };
            var numbers = "1234567";
            if (!directions.Contains(inputArray[0]) || !numbers.Contains(inputArray[1]) || inputArray[1].Length != 1)
                yield break;
            else
            {
                yield return null;
                var button = buttons[Array.IndexOf(directions, inputArray[0])];
                for (int i = 0; i < int.Parse(inputArray[1]); i++)
                {
                    yield return new WaitForSeconds(.1f);
                    button.OnInteract();
                    yield return null;
                    button.OnInteractEnded();
                }
            }
        }
        else if (inputArray.Length == 1)
        {
            if (input == "SUBMIT")
            {
                yield return null;
                submitButton.OnInteract();
            }
            else if (input == "RESET")
            {
                yield return null;
                for (int i = 0; i < 4; i++)
                {
                    if (displayedRotations[i] != originalRotations[i])
                    {
                        yield return null;
                        buttons[i].OnInteract();
                        yield return new WaitUntil(() => longPresses[i]);
                        buttons[i].OnInteractEnded();
                    }
                }
            }
            else
                yield break;
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        for (int i = 0; i < 4; i++)
        {
            while (displayedRotations[i] != solution[i])
            {
                yield return null;
                buttons[i].OnInteract();
                yield return null;
                buttons[i].OnInteractEnded();
            }
        }
        yield return null;
        submitButton.OnInteract();
    }
}
