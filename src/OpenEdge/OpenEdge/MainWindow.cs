using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Buttplug.Client;
using Buttplug.Core.Messages;
using OpenEdge.helper;
using OpenEdge.scripts;
using OpenEdge.vocab;
using Microsoft.Win32;
using NAudio.Wave;

namespace OpenEdge;

public partial class MainWindow : Page, IComponentConnector
{
	private double talkSpeed = 3.0;

	public float bpm;

	private float maxBpm = 420f;

	private float minBpm = 10f;

	private List<string> imagePaths = new List<string>();

	private List<string> gifPaths = new List<string>();

	private Btn[] talkButtons;

	private int hypnoIntensity = 1;

	private int[] displayedImages;

	private int displayedImagesAmount;

	private Random random = new Random();

	private sealed class ModHookRuntimeContext
	{
		public string HookName { get; init; } = "";

		public string SourceModId { get; init; } = "";

		public string Script { get; init; } = "";

		public string Mode { get; init; } = "";

		public bool Handled { get; set; }
	}

	private readonly Stack<ModHookRuntimeContext> activeModHooks = new Stack<ModHookRuntimeContext>();

	private bool sessionEndModHookCompleted;

	private DevSessionToolsWindow devSessionToolsWindow;

	private int[] ctPointer = new int[2];

	private Storyboard[] textCenterStoryboards;

	private Storyboard spiralStoryBoard = new Storyboard();

	private Storyboard holdBreathStoryBoard = new Storyboard();

	public bool stroking;

	private int strokeAmount;

	private int edgesDone;

	private int totalTimeOnEdge;

	private int petPlayScripts;

	public Page1 p;

	private bool holdingBreath;

	private int beatCount;

	private TextBlock[] textBlocks;

	private CancellationTokenSource source;

	public TalkBaseClass currentScript;

	public bool sessionActive;

	public bool scriptPaused;

	public Stopwatch stopwatch = new Stopwatch();

	private Voc voc;

	public string currentState = "module";

	public Stopwatch watch = new Stopwatch();

	public int timeBeforeBeat = 2000;

	private bool firstHoldingEdge = true;

	private Stopwatch linkStopWatch = new Stopwatch();

	private Stopwatch cbtStopWatch = new Stopwatch();

	private bool linked;

	private int longSilence;

	public int score = 100;

	public int daysSinceFull;

	public double difficulty = 1.0;

	private int promissedEdges;

	private int secondsOfEdging;

	private Stopwatch sessionTimer = new Stopwatch();

	private Stopwatch sessionLogTimer = new Stopwatch();

	private Stopwatch performanceLogTimer = new Stopwatch();

	private TimeSpan lastPerformanceCpuTime;

	private DateTime lastPerformanceSampleUtc = DateTime.UtcNow;

	private Stopwatch boredByImages = new Stopwatch();

	private Ellipse[] ballPitLeft = new Ellipse[24];

	private Ellipse[] ballPitRight = new Ellipse[24];

	private Bluetooth bt;

	private int ballPitCount;

	private bool controlBtLoopStarted;

	private bool strokerLoopStarted;

	private bool strokeLoopStarted;

	private int strokeTickRunning;

	private long lastBallMotionMs;

	private long lastHeartAnimationMs;

	public int[] displayOptions = new int[0];

	private string[] allText = new string[0];

	public double masterVolumeValue = 1.0;

	public double videoVolumeValue = 1.0;

	public double uiVolumeValue = 1.0;

	public double asmrVolumeValue = 1.0;

	public double ttsVolumeValue = 1.0;

	public bool edgeAllowed = true;

	private bool noBalls;

	public int pronouns;

	private bool leftEarly;

	private bool lostConnection;

	public int mood = 50;

	public bool orgasmDenied;

	public int sessionLength = 600;

	private bool subliminal;

	private bool strobeTextRunning;

	private int strobeTextGeneration;

	private readonly object strobeTextLock = new object();

	private ButtplugClientDevice plug;

	private ButtplugClientDevice wand;

	private ButtplugClientDevice ona;

	private double plugSpeed;

	private double wandSpeed;

	private double onaSpeed;

	private bool stopOna = true;

	private int talkingTime = 6000;

	private Storyboard hypnosisOverlayStoryboard = new Storyboard();

	private bool wantsBeatBar = true;

	private int hypnosisCount;

	public bool subliminalAudio;

	private string[] asmrStrings = new string[5] { "moaning", "licking1", "licking2", "licking3", "licking4" };

	private long timeSince;

	private AnglePlayer anglePlayer;

	private bool cancelBreath;

	public LineReader lr;

	private string askedForUserInput = "";

	private string acceptAnswer = "";

	private bool lastMediaWasVideo;

	public SecondWindow secWindow;

	public bool specialAudioLocked;

	private ImageTagger imageTagger;

	private bool sessionPaused;

	public string[] currentTags = new string[0];

	private int hasEdged;

	private bool muteBeatBar;

	public int imageSpeedAdditive;

	private int timed;

	private MediaCatalogService mediaCatalog;

	private readonly CompatibilityStateService compatibilityStateService;

	private readonly SettingsRegistry settingsRegistry;

	private readonly LegacyStateAdapter legacyStateAdapter;

	private readonly SessionFlagStore sessionFlagStore;

    private readonly TtsService tts = new TtsService();

    public MainWindow(SecondWindow sWR, MediaCatalogService mediaCatalog, CompatibilityStateService compatibilityStateService, SettingsRegistry settingsRegistry)
	{
		this.mediaCatalog = mediaCatalog;
		this.compatibilityStateService = compatibilityStateService;
		this.settingsRegistry = settingsRegistry;
		legacyStateAdapter = new LegacyStateAdapter(compatibilityStateService, settingsRegistry);
		sessionFlagStore = new SessionFlagStore();
		setNewSpeed(50f);
		ThreadPool.SetMinThreads(16, 16);
		InitializeComponent();
		textBlocks = new TextBlock[5] { textCenter, textBotRight, textTopLeft, textTopRight, textBotLeft };
		textCenterStoryboards = new Storyboard[2]
		{
			new Storyboard(),
			new Storyboard()
		};
		reloadImagesVideos();
		base.Dispatcher.InvokeAsync(createCenterTextAnimations, DispatcherPriority.SystemIdle);
		holdBreathLoad();
		secWindow = sWR;
		secGrid.Children.Add(secWindow);
		source = new CancellationTokenSource();
		secWindow.setMw(this);
		anglePlayer = new AnglePlayer();
		createBallPit();
	}

	public void preLoad(Page1 p)
	{
		this.p = p;
		imageTagger = p.imageTagger;
		lr = new LineReader(this);
	}

	private void longEdge()
	{
		int edgeNum = hasEdged;
		Task.Run(delegate
		{
			Thread.Sleep(60000);
			if (hasEdged == edgeNum)
			{
				currentScript = new LongEdge(this);
				secWindow.hideCaptionImage();
			}
		});
	}

	public string getSingleCensorText()
	{
		return lr.getVocab("singleCensorText");
	}

	public string strokeMethod()
	{
		string text = "";
		string text2 = "";
		setVar("main", 0.ToString() ?? "");
		setVar("sub", 0.ToString() ?? "");
		int num = random.Next(17);
		if (num <= 8)
		{
			if (num <= 6)
			{
				text += "stroke with your main hand";
				text2 = "off hand";
			}
			else
			{
				text += "stroke with your off hand";
				text2 = "main hand";
			}
		}
		else if (num > 10)
		{
			switch (num)
			{
			case 11:
				text += "stroke with both hands, one strokes the shaft while the other twists around the tip";
				break;
			case 12:
				if (!getFlag("sph"))
				{
					return strokeMethod();
				}
				text += "stroke that dicklet with only two fingers";
				text2 = "off hand";
				currentScript.setFlag("strokeTwist", temp: true);
				setVar("main", 2.ToString() ?? "");
				break;
			case 13:
				text += "stroke with your main hand upside down";
				text2 = "off hand";
				setVar("main", 3.ToString() ?? "");
				break;
			case 14:
				text += "stroke only the head";
				setVar("main", 4.ToString() ?? "");
				text2 = "off hand";
				break;
			case 15:
				text += "stroke only the shaft";
				text2 = "off hand";
				setVar("main", 5.ToString() ?? "");
				break;
			case 16:
				text += "make a tight ring and use that to stroke";
				text2 = "off hand";
				setVar("main", 6.ToString() ?? "");
				break;
			}
		}
		else
		{
			text += "twist your hand as you stroke";
			setVar("main", 1.ToString() ?? "");
			text2 = "off hand";
		}
		if (text2 != "" && random.Next(10) > 7)
		{
			switch (random.Next(5))
			{
			case 0:
				text = text + ", while fondling your balls with your " + text2;
				setVar("sub", 1.ToString() ?? "");
				break;
			case 1:
				text += ", while teasing your left nipple";
				setVar("sub", 2.ToString() ?? "");
				break;
			case 2:
				text += ", while teasing your right nipple";
				setVar("sub", 2.ToString() ?? "");
				break;
			case 3:
				text += ", while squeezing your balls";
				setVar("sub", 1.ToString() ?? "");
				break;
			case 4:
				text += ", while strongly squeezing your balls";
				setVar("sub", 1.ToString() ?? "");
				break;
			}
		}
		return text;
	}

	public void censorCheck(bool forceCensor = false, int censorType = -1)
	{
		if (!isSettingEnabled("censorship") && !getTFlag("censoredSession"))
		{
			return;
		}
		currentScript.deleteFlag("censor", temp: true);
		secWindow.censorMode = 0;
		if ((!getTFlag("noCensors") || getTFlag("censoredSession")) && (mood - getSettingValue("censorIncrease") * 2 < random.Next(102) || getTFlag("censoredSession") || forceCensor))
		{
			currentScript.setFlag("censor", temp: true);
			if (forceCensor)
			{
				secWindow.censorMode = random.Next(1, 3);
			}
			else if (censorType == -1)
			{
				secWindow.censorMode = random.Next(1, 4);
			}
			else
			{
				secWindow.censorMode = censorType;
			}
		}
	}

	public bool setBackground(string backgroundPath)
	{
		if (string.IsNullOrWhiteSpace(backgroundPath))
		{
			return false;
		}
		string text = RuntimePaths.ResolveRuntimePath(backgroundPath);
		if (File.Exists(text))
		{
			BitmapImage img = new BitmapImage();
			img.BeginInit();
			img.UriSource = new Uri(text);
			img.EndInit();
			img.Freeze();
			base.Dispatcher.Invoke(delegate
			{
				secWindow.heartbg.Source = img;
			});
			return true;
		}
		return false;
	}

	public void pickScript()
	{
		SessionTraceLogger.Info("script-picker", "enter elapsed=" + sessionTimer.Elapsed.TotalSeconds + " target=" + sessionLength + " currentScript=" + currentScript?.GetType().Name + " linked=" + linked + " state=" + currentState);
		hideFavor();
		censorCheck();
		unSetTag();
		if (!getTFlag("closeWithoutSession"))
		{
			currentScript.setFlag("failedSessionEnd");
		}
		int remainingSessionSeconds = Math.Max(0, sessionLength - (int)sessionTimer.Elapsed.TotalSeconds);
		setVar("sessionLength", remainingSessionSeconds.ToString() ?? "");
		setVar("totalTimeOnEdge", totalTimeOnEdge.ToString() ?? "");
		setVar("strokeAmount", strokeAmount.ToString() ?? "");
		setVar("edgesDone", edgesDone.ToString() ?? "");
		removeSpecialButtons("Please...", 6);
		if (sessionTimer.Elapsed.TotalSeconds >= (double)sessionLength)
		{
			if (sessionEndModHookCompleted)
			{
				SessionTraceLogger.Info("script-picker", "branch=sessionEndModCompleted");
				return;
			}
			if (!IsSessionEndingScriptActive())
			{
				SessionTraceLogger.Info("script-picker", "branch=sessionEndExpired");
				SelectSessionEndingScript();
				return;
			}
		}
		if (random.Next(10) > 7 && getTFlag("kneel"))
		{
			SessionTraceLogger.Info("script-picker", "branch=kneelNo-random");
			currentScript = new OpenEdge.scripts.KneelNo(this, currentScript);
			return;
		}
		if (!linked)
		{
			SessionTraceLogger.Info("script-picker", "branch=strokeLink");
			strokeLink();
			linked = true;
			return;
		}
		changeMoodBy(1);
		changeRequestBy(-1);
		linked = false;
		currentState = "module";
		if (getTFlag("kneel"))
		{
			SessionTraceLogger.Info("script-picker", "branch=kneelNo");
			currentScript = new OpenEdge.scripts.KneelNo(this, currentScript);
		}
		else if (!settingsRegistry.HasCompletedFirstAnalSession() && isSettingEnabled("anal"))
		{
			SessionTraceLogger.Info("script-picker", "branch=firstAnal");
			currentScript = new OpenEdge.scripts.Anal(this, currentScript);
		}
		else if (!getTFlag("sessionIntro"))
		{
			SessionTraceLogger.Info("script-picker", "branch=sessionIntro");
			if (RunModHookBeforeBase("sessionIntro"))
			{
				setTFlag("sessionIntro");
				return;
			}
			currentScript = new SessionIntro(this);
		}
		else if (!getTFlag("queuedAskHandled"))
		{
			SessionTraceLogger.Info("script-picker", "branch=queuedAskCheck");
			QueueMediaDiscoveredSettingAsks();
			string nextEligibleQueuedSettingAsk = GetNextEligibleQueuedSettingAsk();
			if (!string.IsNullOrWhiteSpace(nextEligibleQueuedSettingAsk))
			{
				currentScript = new AskFlag(this, nextEligibleQueuedSettingAsk);
				currentScript.setFlag("queuedAskHandled", temp: true);
				return;
			}
			SessionTraceLogger.Info("script-picker", "branch=queuedAskNone");
			setTFlag("queuedAskHandled");
			if (sessionTimer.Elapsed.TotalSeconds >= (double)sessionLength)
			{
				SessionTraceLogger.Info("script-picker", "branch=sessionEnd");
				SelectSessionEndingScript();
			}
			else
			{
				SessionTraceLogger.Info("script-picker", "branch=methodPicker");
				methodPicker();
			}
		}
		else if (sessionTimer.Elapsed.TotalSeconds >= (double)sessionLength)
		{
			SessionTraceLogger.Info("script-picker", "branch=sessionEnd");
			SelectSessionEndingScript();
		}
		else
		{
			SessionTraceLogger.Info("script-picker", "branch=methodPicker");
			methodPicker();
		}
	}

	private bool IsSessionEndingScriptActive()
	{
		return currentScript is Ending || currentScript is ChastitySessionEnd || currentScript is PetPlayOff;
	}

	public void DevTriggerMethodPicker()
	{
		SessionTraceLogger.Info("dev-tools", "trigger=methodPicker");
		base.Dispatcher.Invoke(delegate
		{
			PauseStrokeForDevTrigger();
			linked = true;
			currentState = "module";
			methodPicker();
		});
	}

	private void PauseStrokeForDevTrigger()
	{
		if (stroking)
		{
			SessionTraceLogger.Info("dev-tools", "paused stroking for manual trigger bpm=" + bpm + " state=" + currentState);
		}
		stroking = false;
		subliminal = false;
		removeSpecialButtons("I'm on the edge", 1);
		removeSpecialButtons("I came", 3);
	}

	public void DevTriggerSessionEnd()
	{
		SessionTraceLogger.Info("dev-tools", "trigger=sessionEnd");
		base.Dispatcher.Invoke(delegate
		{
			PauseStrokeForDevTrigger();
			sessionEndModHookCompleted = false;
			SelectSessionEndingScript();
		});
	}

	public void DevTriggerHook(string hookName)
	{
		hookName = (hookName ?? "").Trim();
		if (hookName.Length == 0)
		{
			return;
		}
		SessionTraceLogger.Info("dev-tools", "trigger=hook hook=" + hookName);
		base.Dispatcher.Invoke(delegate
		{
			PauseStrokeForDevTrigger();
			RunModHookFromScript(hookName);
		});
	}

	public void DevTriggerScript(string scriptName)
	{
		scriptName = (scriptName ?? "").Trim();
		if (scriptName.Length == 0)
		{
			return;
		}
		SessionTraceLogger.Info("dev-tools", "trigger=script script=" + scriptName);
		base.Dispatcher.Invoke(delegate
		{
			PauseStrokeForDevTrigger();
			GenericScript script = new GenericScript(this, currentScript, scriptName);
			if (script.allText.Length == 0)
			{
				SessionTraceLogger.Info("dev-tools", "script skipped reason=missing-or-empty script=" + scriptName);
				return;
			}
			currentScript = script;
			currentState = "module";
		});
	}

	public void DevTriggerEdgeOpportunity()
	{
		DevTriggerHook("edgeOpportunity");
	}

	public void DevTriggerOrgasmDecision()
	{
		DevTriggerHook("orgasmDecision");
	}

	private void SelectSessionEndingScript()
	{
		SessionTraceLogger.Info("session", "session time expired elapsed=" + sessionTimer.Elapsed.TotalSeconds + " target=" + sessionLength + " currentScript=" + currentScript?.GetType().Name);
		if (RunModHookBeforeBase("sessionEnd"))
		{
			return;
		}
		if (getTFlag("petPlay"))
		{
			currentScript = new PetPlayOff(this);
		}
		else if (!isSettingEnabled("wearingChastity"))
		{
			currentScript = new Ending(this);
		}
		else
		{
			currentScript = new ChastitySessionEnd(this);
		}
	}

	public void changeRequestBy(int requestChange)
	{
		int num = requestChange + int.Parse(getVar("request"));
		if (num < 10)
		{
			num = 10;
		}
		if (num > 120)
		{
			num = 120;
		}
		setVar("request", num.ToString() ?? "");
	}

	public void changeMoodBy(int moodChange)
	{
		mood += moodChange;
		if (mood < 0)
		{
			mood = 0;
		}
		if (mood > 100)
		{
			mood = 100;
		}
		secWindow.censorIntensity = (float)((mood - 100) * -1) / 100f + 0.2f + (float)((double)getSettingValue("censorIncrease") * 0.2);
		setVar("mood", mood.ToString() ?? "");
	}

	private void methodPicker()
	{
		SessionTraceLogger.Info("script-picker", "methodPicker entered petPlay=" + getTFlag("petPlay") + " wearingChastity=" + isSettingEnabled("wearingChastity") + " elapsed=" + sessionTimer.Elapsed.TotalSeconds);
		if (RunMethodPickerModHook())
		{
			return;
		}
		if (!getTFlag("petPlay") && !isSettingEnabled("wearingChastity"))
		{
			switch (random.Next(100))
			{
			case 0:
				if (sessionTimer.Elapsed.TotalMinutes > 3.0)
				{
					if (random.Next(100) > 92 && isSettingEnabled("cuck") && !getTFlag("cuck") && !settingsRegistry.HasActiveCuckFridayCooldown())
					{
						currentScript = new Cuck(this);
					}
					else
					{
						currentScript = new NoTouch(this);
					}
					return;
				}
				break;
			case 1:
				if (!getTFlag("hypnosis") && sessionTimer.Elapsed.TotalMinutes >= 2.0 && isSettingEnabled("hypno"))
				{
					currentScript = new Hypnosis(this);
					return;
				}
				break;
			case 2:
				if (sessionTimer.Elapsed.TotalMinutes > 2.0 && !getTFlag("game"))
				{
					currentScript = new Game(this);
					return;
				}
				break;
			case 3:
				currentScript = new StrokeStories(this);
				return;
			case 4:
				if (sessionTimer.Elapsed.TotalMinutes > 5.0)
				{
					currentScript = new Edging(this);
					return;
				}
				break;
			case 5:
				if (!getTFlag("anal") && isSettingEnabled("anal") && settingsRegistry.HasActiveAnalTraining() && sessionTimer.Elapsed.TotalMinutes > 4.0)
				{
					currentScript = new OpenEdge.scripts.Anal(this, currentScript);
					return;
				}
				break;
			case 6:
				if (sessionTimer.Elapsed.TotalMinutes > 2.0 && sessionLength > 300)
				{
					SelectChangeStateScript();
					return;
				}
				break;
			case 8:
				if (mood < 20 && sessionTimer.Elapsed.TotalMinutes > 3.0)
				{
					currentScript = new Punishment(this, currentScript);
					return;
				}
				break;
			case 9:
				if (mood > 85 && !getTFlag("reward") && sessionTimer.Elapsed.TotalMinutes > 8.0 && sessionLength > 300)
				{
					currentScript = new Reward(this);
					return;
				}
				break;
			case 10:
				if (sessionTimer.Elapsed.TotalMinutes > 2.0)
				{
					currentScript = new AskFlag(this);
					return;
				}
				break;
			case 11:
				if (sessionTimer.Elapsed.TotalMinutes > 4.0 && isPetPlayAdvancedEnabled() && !getTFlag("petPlayDone"))
				{
					currentScript = new PetPlayOn(this);
					return;
				}
				break;
			case 12:
				if (!getTFlag("LOBScript") && getFlag("LOB") && getFlagTimeDays("LOB") >= 1)
				{
					currentScript = new LOB(this);
					return;
				}
				break;
			}
		}
		else if (petPlayScripts < random.Next(4, 60) && !isSettingEnabled("wearingChastity"))
		{
			switch (random.Next(100))
			{
			case 0:
				currentScript = new NoTouch(this);
				petPlayScripts++;
				return;
			case 1:
				if (!getTFlag("hypnosis") && isSettingEnabled("hypno"))
				{
					currentScript = new Hypnosis(this);
					petPlayScripts++;
					return;
				}
				break;
			case 2:
				currentScript = new StrokeStories(this);
				petPlayScripts++;
				return;
			case 3:
				if (sessionTimer.Elapsed.TotalMinutes > 5.0)
				{
					currentScript = new Edging(this);
					petPlayScripts++;
					return;
				}
				break;
			case 4:
				if (!getTFlag("anal") && isSettingEnabled("anal") && settingsRegistry.HasActiveAnalTraining())
				{
					currentScript = new OpenEdge.scripts.Anal(this, currentScript);
					petPlayScripts++;
					return;
				}
				break;
			}
		}
		else
		{
			if (!isSettingEnabled("wearingChastity"))
			{
				currentScript = new PetPlayOff(this);
				return;
			}
			if (!isSettingEnabled("wearingChastity"))
			{
				currentScript = new Ending(this);
				return;
			}
			currentState = "module";
			switch (random.Next(100))
			{
			case 0:
				chastityTaunt();
				return;
			case 1:
				currentScript = new ChastityTaunt(this);
				return;
			case 2:
				if (isSettingEnabled("anal"))
				{
					currentScript = new OpenEdge.scripts.Anal(this, currentScript);
					if (isSettingEnabled("vibrator") || getTFlag("vibe"))
					{
						currentState = "vibeNo";
					}
					return;
				}
				break;
			case 3:
				sessionLength += -50;
				break;
			case 4:
				currentScript = new AskFlag(this);
				if (isSettingEnabled("vibrator") || getTFlag("vibe"))
				{
					currentState = "vibeNo";
				}
				return;
			case 5:
				if (sessionTimer.Elapsed.TotalMinutes > 1.0)
				{
					currentScript = new Game(this);
					return;
				}
				break;
			case 6:
				if (sessionTimer.Elapsed.TotalMinutes > 1.0 && sessionLength > 240)
				{
					SelectChangeStateScript();
					return;
				}
				break;
			case 8:
				if (mood < 20 && sessionTimer.Elapsed.TotalMinutes > 3.0)
				{
					currentScript = new Punishment(this, currentScript);
					if (isSettingEnabled("vibrator") || getTFlag("vibe"))
					{
						currentState = "vibeNo";
					}
					return;
				}
				break;
			case 12:
				if (!getTFlag("LOBScript") && getFlag("LOB") && getFlagTimeDays("LOB") >= 1)
				{
					currentScript = new LOB(this);
					return;
				}
				break;
			}
		}
		methodPicker();
	}

	private void SelectChangeStateScript()
	{
		if (RunModHookBeforeBase("changeState"))
		{
			return;
		}
		currentScript = new ChangeState(this);
	}

	private bool RunMethodPickerModHook()
	{
		return RunModHookWithBasePool("methodPicker", baseWeight: 100);
	}

	private bool RunModHookBeforeBase(string hookName)
	{
		return RunModHook(hookName, beforeBase: true);
	}

	public bool RunModHookFromScript(string hookName)
	{
		return RunModHook(hookName, beforeBase: false);
	}

	private bool RunModHookWithBasePool(string hookName, int baseWeight)
	{
		hookName = (hookName ?? "").Trim();
		if (hookName.Length == 0)
		{
			return false;
		}
		if (activeModHooks.Count >= 20)
		{
			SessionTraceLogger.Info("mod-hooks", "skipped hook=" + hookName + " reason=max-depth");
			return false;
		}
		if (activeModHooks.Any((ModHookRuntimeContext context) => string.Equals(context.HookName, hookName, StringComparison.OrdinalIgnoreCase)))
		{
			SessionTraceLogger.Info("mod-hooks", "skipped hook=" + hookName + " reason=reentrant");
			return false;
		}
		List<ModHookDefinition> eligibleHooks = GetEligibleModHooks(hookName);
		List<ModHookDefinition> beforeBaseHooks = eligibleHooks.Where(IsBeforeBaseHookMode).ToList();
		if (TryRunSelectedModHook(hookName, beforeBaseHooks))
		{
			return true;
		}
		List<ModHookDefinition> additiveHooks = eligibleHooks.Where(IsAdditiveHookMode).ToList();
		if (additiveHooks.Count == 0)
		{
			return false;
		}
		int totalWeight = Math.Max(1, baseWeight) + additiveHooks.Sum((ModHookDefinition hook) => Math.Max(1, hook.Weight));
		int pick = random.Next(totalWeight);
		if (pick < Math.Max(1, baseWeight))
		{
			SessionTraceLogger.Info("mod-hooks", "hook=" + hookName + " selected=base additive=" + additiveHooks.Count + " baseWeight=" + baseWeight);
			return false;
		}
		ModHookDefinition selected = PickWeightedModHook(additiveHooks);
		return TryRunSelectedModHook(hookName, new List<ModHookDefinition> { selected });
	}

	private bool RunModHook(string hookName, bool beforeBase)
	{
		hookName = (hookName ?? "").Trim();
		if (hookName.Length == 0)
		{
			return false;
		}
		if (activeModHooks.Count >= 20)
		{
			SessionTraceLogger.Info("mod-hooks", "skipped hook=" + hookName + " reason=max-depth");
			return false;
		}
		if (activeModHooks.Any((ModHookRuntimeContext context) => string.Equals(context.HookName, hookName, StringComparison.OrdinalIgnoreCase)))
		{
			SessionTraceLogger.Info("mod-hooks", "skipped hook=" + hookName + " reason=reentrant");
			return false;
		}
		List<ModHookDefinition> eligibleHooks = GetEligibleModHooks(hookName);
		if (beforeBase)
		{
			eligibleHooks = eligibleHooks.Where(IsBeforeBaseHookMode).ToList();
		}
		if (TryRunSelectedModHook(hookName, eligibleHooks))
		{
			return true;
		}
		SessionTraceLogger.Info("mod-hooks", "hook=" + hookName + " eligible=0 beforeBase=" + beforeBase);
		return false;
	}

	private bool TryRunSelectedModHook(string hookName, List<ModHookDefinition> eligibleHooks)
	{
		while (eligibleHooks.Count > 0)
		{
			ModHookDefinition selected = SelectModHook(eligibleHooks);
			GenericScript script = new GenericScript(this, currentScript, selected.Script, hookName);
			if (script.allText.Length == 0)
			{
				SessionTraceLogger.Info("mod-hooks", "skipped hook=" + hookName + " mod=" + selected.SourceModId + " script=" + selected.Script + " reason=missing-or-empty-script");
				eligibleHooks.Remove(selected);
				continue;
			}
			BeginModHook(hookName, selected);
			currentScript = script;
			currentState = "module";
			return true;
		}
		return false;
	}

	private List<ModHookDefinition> GetEligibleModHooks(string hookName)
	{
		List<ModHookDefinition> eligibleHooks = ModService.GetEnabledHookDefinitions().Where(delegate(ModHookDefinition hook)
		{
			return string.Equals(hook.Hook, hookName, StringComparison.OrdinalIgnoreCase) && IsModHookEligible(hook);
		}).ToList();
		SessionTraceLogger.Info("mod-hooks", "hook=" + hookName + " eligible=" + eligibleHooks.Count);
		return eligibleHooks;
	}

	private static bool IsBeforeBaseHookMode(ModHookDefinition hook)
	{
		return string.Equals(hook.Mode, "exclusive", StringComparison.OrdinalIgnoreCase) || string.Equals(hook.Mode, "replace", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsAdditiveHookMode(ModHookDefinition hook)
	{
		return string.Equals(hook.Mode, "additive", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(hook.Mode);
	}

	private ModHookDefinition SelectModHook(List<ModHookDefinition> hooks)
	{
		List<ModHookDefinition> overrideHooks = hooks.Where((ModHookDefinition hook) => string.Equals(hook.Mode, "replace", StringComparison.OrdinalIgnoreCase) || string.Equals(hook.Mode, "exclusive", StringComparison.OrdinalIgnoreCase)).ToList();
		if (overrideHooks.Count > 0)
		{
			return overrideHooks[0];
		}
		List<ModHookDefinition> additiveHooks = hooks.Where((ModHookDefinition hook) => string.Equals(hook.Mode, "additive", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(hook.Mode)).ToList();
		if (additiveHooks.Count > 0)
		{
			return PickWeightedModHook(additiveHooks);
		}
		List<ModHookDefinition> fallbackHooks = hooks.Where((ModHookDefinition hook) => string.Equals(hook.Mode, "fallback", StringComparison.OrdinalIgnoreCase)).ToList();
		if (fallbackHooks.Count > 0)
		{
			return fallbackHooks[0];
		}
		return hooks[0];
	}

	private void BeginModHook(string hookName, ModHookDefinition selected)
	{
		activeModHooks.Push(new ModHookRuntimeContext
		{
			HookName = hookName,
			SourceModId = selected.SourceModId,
			Script = selected.Script,
			Mode = selected.Mode
		});
		SessionTraceLogger.Info("mod-hooks", "hook=" + hookName + " selected mod=" + selected.SourceModId + " script=" + selected.Script + " mode=" + selected.Mode + " depth=" + activeModHooks.Count);
	}

	public void CompleteModHook(string hookName)
	{
		if (activeModHooks.Count == 0)
		{
			SessionTraceLogger.Info("mod-hooks", "complete hook=" + hookName + " with no active context");
			return;
		}
		ModHookRuntimeContext context = activeModHooks.Pop();
		if (!string.Equals(context.HookName, hookName, StringComparison.OrdinalIgnoreCase))
		{
			SessionTraceLogger.Info("mod-hooks", "complete mismatch requested=" + hookName + " active=" + context.HookName);
		}
		if (context.Handled && string.Equals(context.HookName, "sessionEnd", StringComparison.OrdinalIgnoreCase))
		{
			sessionEndModHookCompleted = true;
		}
		SessionTraceLogger.Info("mod-hooks", "complete hook=" + context.HookName + " mod=" + context.SourceModId + " script=" + context.Script + " handled=" + context.Handled);
		if (context.Handled && string.Equals(context.HookName, "sessionEnd", StringComparison.OrdinalIgnoreCase))
		{
			CompleteSessionEndFromModHook(context.SourceModId);
		}
	}

	private void CompleteSessionEndFromModHook(string modId)
	{
		SessionTraceLogger.Info("session", "session end completed by mod hook mod=" + modId);
		if (!getTFlag("closeWithoutSession"))
		{
			foreach (string file in Directory.GetFiles(RuntimePaths.TempFlagsDir))
			{
				File.Delete(file);
			}
			currentScript.setFlag("sessionEnd");
			currentScript.incrementFlag("sessions");
			setVar("sessionLength", "0");
		}
		sessionActive = false;
		endSession();
	}

	public void MarkCurrentModHookHandled(string reason = "explicit")
	{
		if (activeModHooks.Count == 0)
		{
			SessionTraceLogger.Info("mod-hooks", "handled requested with no active context reason=" + reason);
			return;
		}
		ModHookRuntimeContext context = activeModHooks.Peek();
		context.Handled = true;
		SessionTraceLogger.Info("mod-hooks", "handled hook=" + context.HookName + " mod=" + context.SourceModId + " reason=" + reason);
	}

	private bool IsModHookEligible(ModHookDefinition hook)
	{
		if (isSettingEnabled("wearingChastity") && !hook.AllowedWhileChaste)
		{
			return false;
		}
		if (hook.AllowedStates != null && hook.AllowedStates.Count > 0 && !hook.AllowedStates.Any((string state) => string.Equals(state?.Trim(), currentState, StringComparison.OrdinalIgnoreCase)))
		{
			return false;
		}
		foreach (string setting in hook.RequiresSettings ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(setting) && !isSettingEnabled(setting.Trim()))
			{
				return false;
			}
		}
		foreach (string setting in hook.ForbidsSettings ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(setting) && isSettingEnabled(setting.Trim()))
			{
				return false;
			}
		}
		foreach (string context in hook.RequiresContexts ?? new List<string>())
		{
			if (!string.IsNullOrWhiteSpace(context) && !DerivedContextService.IsContextActive(this, context.Trim()))
			{
				return false;
			}
		}
		List<string> mediaTags = (hook.RequiresMediaTags ?? new List<string>()).Where((string tag) => !string.IsNullOrWhiteSpace(tag)).Select((string tag) => tag.Trim()).ToList();
		if (mediaTags.Count > 0 && !atLeastXMedia(string.Join(",", mediaTags), hook.MinimumMedia <= 0 ? 2 : hook.MinimumMedia))
		{
			return false;
		}
		return true;
	}

	private ModHookDefinition PickWeightedModHook(List<ModHookDefinition> hooks)
	{
		int totalWeight = hooks.Sum((ModHookDefinition hook) => Math.Max(1, hook.Weight));
		int pick = random.Next(totalWeight);
		foreach (ModHookDefinition hook in hooks)
		{
			pick -= Math.Max(1, hook.Weight);
			if (pick < 0)
			{
				return hook;
			}
		}
		return hooks[0];
	}

	// Compatibility API for old scripts and legacy progression state. New setting-like code should use SettingsRegistry helpers.
	public bool getFlag(string flagName)
	{
		return legacyStateAdapter.HasFlag(flagName);
	}

	public double getFlagTime(string flagName)
	{
		return legacyStateAdapter.GetFlagAgeSeconds(flagName);
	}

	public int getFlagTimeDays(string flagName)
	{
		double num = getFlagTime(flagName) / 86400.0;
		if (num % 1.0 > 0.6)
		{
			num += 1.0;
		}
		return (int)num;
	}

	public int getFlagTimeHours(string flagName)
	{
		double num = getFlagTime(flagName) / 3600.0;
		if (num % 1.0 > 0.8)
		{
			num += 1.0;
		}
		return (int)num;
	}

	public bool getFlagAsked(string flagName)
	{
		return legacyStateAdapter.HasAnsweredFlag(flagName);
	}

	// Compatibility API for old FLAGT/ISFLAGT script flow markers. Temp flags intentionally stay outside SettingsRegistry.
	public bool getTFlag(string flagName)
	{
		return sessionFlagStore.Exists(flagName);
	}

	public void setTFlag(string flagName)
	{
		sessionFlagStore.Set(flagName, DateTime.Now.ToString());
	}

	public void deleteTFlag(string flagName)
	{
		sessionFlagStore.Delete(flagName);
	}

	// Compatibility API for old SETVAR/ADDVAR paths. Canonical setting value writes promote through LegacyStateAdapter.
	public void setVar(string name, string value, int retryAttempts = 0)
	{
		if (retryAttempts == 0)
		{
			SessionTraceLogger.Info("state-write", "persistent var " + name + "=" + value);
		}
		Thread.Sleep(2 * retryAttempts);
		try
		{
			if (retryAttempts < 10)
			{
				legacyStateAdapter.SetVar(name, value);
			}
		}
		catch
		{
			setVar(name, value, retryAttempts++);
		}
	}

	protected virtual bool IsFileLocked(FileInfo file)
	{
		try
		{
			using FileStream fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
			fileStream.Close();
			return false;
		}
		catch (IOException)
		{
			return true;
		}
	}

	// Compatibility API for old GETVAR/ISVAR paths. New code should prefer typed canonical accessors where available.
	public string getVar(string name, string errorValue = "0")
	{
		_ = name == "petName";
		try
		{
			return legacyStateAdapter.GetVar(name, errorValue);
		}
		catch
		{
			Thread.Sleep(100);
			return getVar(name, errorValue);
		}
	}

	// Compatibility API for old FLAG: script writes. New scripts should use canonical SET* commands.
	public void setPersistentFlag(string flagName)
	{
		legacyStateAdapter.SetFlag(flagName);
	}

	// Compatibility API for old DELFLAG: script writes. New scripts should use canonical SET* commands or structured clears.
	public void deletePersistentFlag(string flagName)
	{
		legacyStateAdapter.DeleteFlag(flagName);
	}

	public void queueSettingAsk(string key)
	{
		settingsRegistry.QueueAsk(key);
	}

	public void dequeueSettingAsk(string key)
	{
		settingsRegistry.DequeueAsk(key);
	}

	public bool isSettingAskQueued(string key)
	{
		return settingsRegistry.IsAskQueued(key);
	}

	public bool isSettingEnabled(string key)
	{
		if (settingsRegistry.GetDefinition(key) == null)
		{
			return getFlag(key);
		}
		return settingsRegistry.IsEnabled(key);
	}

	public bool isSettingAnswered(string key)
	{
		if (settingsRegistry.GetDefinition(key) == null)
		{
			return getFlagAsked(key);
		}
		return settingsRegistry.IsAnswered(key);
	}

	public bool isSettingDeclined(string key)
	{
		return isSettingAnswered(key) && !isSettingEnabled(key);
	}

	public bool isAnalStage(string stage)
	{
		return settingsRegistry.IsAnalStage(stage);
	}

	public bool isAnalPreference(string preference)
	{
		return settingsRegistry.IsAnalPreference(preference);
	}

	public bool hasCompletedFirstAnalSession()
	{
		return settingsRegistry.HasCompletedFirstAnalSession();
	}

	public bool hasActiveAnalTraining()
	{
		return settingsRegistry.HasActiveAnalTraining();
	}

	public bool hasDeclinedAnalTraining()
	{
		return settingsRegistry.HasDeclinedAnalTraining();
	}

	public bool isPetPlayAdvancedEnabled()
	{
		return settingsRegistry.IsPetPlayAdvancedEnabled();
	}

	public bool isPetPlayAdvancedDeclined()
	{
		return settingsRegistry.IsPetPlayAdvancedDeclined();
	}

	public bool isOutsideSessionRuleActive(string key)
	{
		return settingsRegistry.IsOutsideSessionRuleActive(key);
	}

	public bool isLobRuntimeEnabled()
	{
		return settingsRegistry.IsLobRuntimeEnabled();
	}

	public bool hasSettingText(string key)
	{
		return settingsRegistry.HasTextValue(key);
	}

	public int getSettingValue(string key, int defaultValue = 0)
	{
		if (settingsRegistry.GetDefinition(key) == null)
		{
			return int.TryParse(getVar(key, defaultValue.ToString()), out int result) ? result : defaultValue;
		}
		return settingsRegistry.GetNumericValue(key, defaultValue);
	}

	public string getSettingText(string key)
	{
		return settingsRegistry.GetRawValue(key) ?? "";
	}

	public void setSettingEnabled(string key, bool enabled)
	{
		settingsRegistry.SetEnabled(key, enabled);
	}

	public void setSettingValue(string key, int value)
	{
		settingsRegistry.SetNumericValue(key, value);
	}

	public void setSettingText(string key, string value)
	{
		settingsRegistry.SetRawValue(key, value);
	}

	public void setAnalStage(string stage)
	{
		AnalSettingState state = settingsRegistry.GetAnalState();
		state.Enabled = true;
		state.Answered = true;
		string normalized = (stage ?? "").Trim();
		if (string.Equals(normalized, "first", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "firstComplete", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "firstCompleted", StringComparison.OrdinalIgnoreCase))
		{
			state.FirstSessionCompleted = true;
		}
		else if (string.Equals(normalized, "beginner", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "experienced", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "unknown", StringComparison.OrdinalIgnoreCase))
		{
			state.Experience = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
		}
		settingsRegistry.SaveAnalState(state);
	}

	public void setAnalPreference(string preference)
	{
		AnalSettingState state = settingsRegistry.GetAnalState();
		state.Enabled = true;
		state.Answered = true;
		string normalized = (preference ?? "").Trim();
		if (string.Equals(normalized, "like", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "neutral", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "dislike", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "unknown", StringComparison.OrdinalIgnoreCase))
		{
			state.Preference = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
		}
		settingsRegistry.SaveAnalState(state);
	}

	public void setAnalTraining(string value)
	{
		AnalSettingState state = settingsRegistry.GetAnalState();
		state.Enabled = true;
		state.Answered = true;
		string normalized = (value ?? "").Trim();
		state.TrainingEnabled = string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) || normalized == "1";
		state.TrainingDeclined = string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "declined", StringComparison.OrdinalIgnoreCase) || normalized == "0";
		settingsRegistry.SaveAnalState(state);
	}

	public void setPetPersona(string persona)
	{
		PetPlaySettingState state = settingsRegistry.GetPetPlayState();
		state.Enabled = true;
		state.Answered = true;
		string normalized = (persona ?? "").Trim();
		state.Persona = string.Equals(normalized, "pup", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "puppy", StringComparison.OrdinalIgnoreCase) ? "Pup" : (string.Equals(normalized, "cat", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "kitten", StringComparison.OrdinalIgnoreCase) ? "Cat" : "None");
		settingsRegistry.SavePetPlayState(state);
	}

	public void setOutsideRule(string key, int value)
	{
		OutsideSessionSettingState state = settingsRegistry.GetOutsideSessionState();
		state.Enabled = true;
		state.Answered = true;
		switch ((key ?? "").Trim())
		{
		case "noPorn":
			state.NoPornRemaining = value;
			break;
		case "constantCei":
			state.ConstantCeiRemaining = value;
			break;
		case "plugHour":
			state.PlugHourRemaining = value;
			break;
		case "watchPorn":
			state.WatchPornRemaining = value;
			break;
		case "hypnoFiles":
			state.HypnoFilesRemaining = value;
			break;
		default:
			return;
		}
		settingsRegistry.SaveOutsideSessionState(state);
	}

	public void setLobWindow(int earlyHour, int lateHour)
	{
		LobSettingState state = settingsRegistry.GetLobState();
		state.Enabled = true;
		state.Answered = true;
		state.EarlyHour = earlyHour;
		state.LateHour = lateHour;
		settingsRegistry.SaveLobState(state);
	}

	public void setCuckStage(int stage)
	{
		CuckSettingState state = settingsRegistry.GetCuckState();
		state.Enabled = true;
		state.Answered = true;
		state.Stage = stage;
		settingsRegistry.SaveCuckState(state);
	}

	public void setCuckFriday(bool passed)
	{
		CuckSettingState state = settingsRegistry.GetCuckState();
		state.Enabled = true;
		state.Answered = true;
		state.FridayPassed = passed;
		settingsRegistry.SaveCuckState(state);
	}

	public void setChastityState(string key, string value)
	{
		ChastitySettingState state = settingsRegistry.GetChastityState();
		state.Enabled = true;
		state.Answered = true;
		bool boolValue = IsScriptTrue(value);
		switch ((key ?? "").Trim())
		{
		case "cage":
		case "owned":
			state.CageOwned = boolValue;
			break;
		case "wearing":
		case "wearingChastity":
			state.WearingCage = boolValue;
			break;
		case "cageType":
		case "type":
			state.CageType = value ?? "";
			break;
		case "vibrator":
			state.VibratorOwned = boolValue;
			break;
		case "lostKey":
			state.LostKey = boolValue;
			break;
		case "toldAboutNecklace":
		case "necklace":
			state.ToldAboutNecklace = boolValue;
			break;
		case "duration":
		case "durationDays":
		case "chastityTime":
			if (int.TryParse(value, out int durationDays))
			{
				state.DurationDays = durationDays;
			}
			break;
		case "startDate":
		case "date":
		case "chastityDate":
			state.StartDateText = value ?? "";
			break;
		default:
			return;
		}
		settingsRegistry.SaveChastityState(state);
	}

	public void setCensorIntensity(int intensity)
	{
		CensorshipSettingState state = settingsRegistry.GetCensorshipState();
		state.Enabled = true;
		state.Answered = true;
		state.Intensity = intensity;
		settingsRegistry.SaveCensorshipState(state);
	}

	public void setBreathTime(int seconds)
	{
		BreathPlaySettingState state = settingsRegistry.GetBreathPlayState();
		state.Enabled = true;
		state.Answered = true;
		state.BreathTimeSeconds = seconds;
		settingsRegistry.SaveBreathPlayState(state);
	}

	private static bool IsScriptTrue(string value)
	{
		string normalized = (value ?? "").Trim();
		if (normalized.Length == 0)
		{
			return false;
		}
		normalized = normalized.Split(new char[0], StringSplitOptions.RemoveEmptyEntries)[0];
		return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase) || normalized == "1";
	}

	private void QueueMediaDiscoveredSettingAsks()
	{
		foreach (SettingDefinition definition in settingsRegistry.GetDefinitions())
		{
			if (!definition.QueueableAsk || definition.MediaDiscoveryTags.Count == 0 || settingsRegistry.IsAnswered(definition.Key) || settingsRegistry.IsAskQueued(definition.Key))
			{
				continue;
			}
			string tagString = string.Join(",", definition.MediaDiscoveryTags);
			if (atLeastXMedia(tagString, definition.MediaDiscoveryMinimum))
			{
				settingsRegistry.QueueAsk(definition.Key);
				SessionTraceLogger.Info("media-discovery", "queued ask key=" + definition.Key + " tags=" + tagString + " minimum=" + definition.MediaDiscoveryMinimum);
			}
		}
	}

	public bool TryStartExtraSettingAsk()
	{
		QueueMediaDiscoveredSettingAsks();
		string nextEligibleQueuedSettingAsk = GetNextEligibleQueuedSettingAsk();
		if (!string.IsNullOrWhiteSpace(nextEligibleQueuedSettingAsk))
		{
			SessionTraceLogger.Info("queued-ask", "user requested extra ask key=" + nextEligibleQueuedSettingAsk);
			currentScript = new AskFlag(this, nextEligibleQueuedSettingAsk);
			return true;
		}
		SessionTraceLogger.Info("queued-ask", "user requested extra ask; falling back to normal AskFlag pool");
		currentScript = new AskFlag(this);
		return currentScript?.allText != null && currentScript.allText.Length > 0;
	}

	private string GetNextEligibleQueuedSettingAsk()
	{
		FinalizeQueuedSettingAsks();
		foreach (string queuedAskKey in settingsRegistry.GetQueuedAskKeys())
		{
			if (IsQueuedSettingAskEligible(queuedAskKey))
			{
				return queuedAskKey;
			}
		}
		return "";
	}

	private void FinalizeQueuedSettingAsks()
	{
		foreach (string queuedAskKey in settingsRegistry.GetQueuedAskKeys().ToList())
		{
			if (getFlagAsked(queuedAskKey))
			{
				settingsRegistry.DequeueAsk(queuedAskKey);
			}
		}
	}

	public bool IsQueuedSettingAskEligible(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || getFlagAsked(key))
		{
			return false;
		}
		switch (key)
		{
		case "safeWord":
		case "virgin":
		case "string":
		case "anal":
		case "palming":
		case "taskScreen":
		case "asmr":
		case "clothesPins":
		case "hypno":
		case "breathPlay":
		case "canRemove":
		case "censorship":
			return true;
		case "hands":
			return atLeastXMedia("Hands", 1);
		case "gay":
			return atLeastXMedia("Cock", 1);
		case "feet":
			return atLeastXMedia("Feet", 1) || atLeastXMedia("feet", 1);
		case "findom":
			return !isSettingEnabled("wearingChastity") && int.Parse(getVar("totalTribute")) > 10;
		case "cei":
		case "humiliation":
		case "edgeIntro":
		case "cockControl":
		case "outsideSession":
		case "LOB":
		case "petPlay":
			return !isSettingEnabled("wearingChastity");
		case "cuck":
			return !isSettingEnabled("wearingChastity") && atLeastXMedia("Cuckold", 1);
		case "petPlayAdvanced":
			return isSettingEnabled("petPlay") && isSettingEnabled("collar") && isSettingEnabled("treats");
		default:
			SettingDefinition definition = settingsRegistry.GetDefinition(key);
			if (definition?.MediaDiscoveryTags.Count > 0)
			{
				return atLeastXMedia(string.Join(",", definition.MediaDiscoveryTags), definition.MediaDiscoveryMinimum);
			}
			return false;
		}
	}

	public void ApplyPronounSetting(int pronounValue)
	{
		switch (pronounValue)
		{
		case 1:
		case 2:
			pronouns = pronounValue;
			break;
		default:
			pronouns = 0;
			break;
		}
		setVar("pronoun", pronouns.ToString() ?? "");
	}

	private List<string> Shuffle(List<string> array)
	{
		int count = array.Count;
		for (int i = 0; i < count - 1; i++)
		{
			int index = i + random.Next(count - i);
			string value = array[index];
			array[index] = array[i];
			array[i] = value;
		}
		return array;
	}

	public void optionsScreen(Page1 p)
	{
		secWindow.muteVideo(p.muteVideos);
		if (p.hideBeatBar)
		{
			hideBeatbar();
			wantsBeatBar = false;
		}
		ApplyPronounSetting(p.pronounsInt);
		this.p = p;
	}

	public void reloadImagesVideos()
	{
		if (currentScript == null)
		{
			return;
		}
		Task.Run(delegate
		{
			mediaCatalog.Reload();
			imagePaths = mediaCatalog.GetActiveImagePaths();
			gifPaths = mediaCatalog.GetActiveGifPaths();
			secWindow.videoPaths = Shuffle(mediaCatalog.GetActiveVideoPaths());
			secWindow.gifPaths = Shuffle(gifPaths);
			imagePaths = Shuffle(imagePaths);
			displayOptions = new int[0];
			displayedImages = new int[imagePaths.Count];
			Array.Fill(displayedImages, -1);
			sortImages();
		});
	}

	private void controlBt(bool isMain = false)
	{
		if (currentScript != null && !sessionPaused)
		{
			if (!bt.stillConnected(plug, 0))
			{
				plug = null;
			}
			if (!bt.stillConnected(wand, 0))
			{
				wand = null;
			}
			if (!bt.stillConnected(ona, 0))
			{
				ona = null;
			}
			if (plug == null)
			{
				plug = bt.getOnePlug();
				if (plug != null)
				{
					currentScript.setFlag("bluetoothPlug", temp: true);
				}
				else
				{
					currentScript.deleteFlag("bluetoothPlug", temp: true);
				}
			}
			if (wand == null)
			{
				wand = bt.getOneWand();
				if (wand != null)
				{
					currentScript.setFlag("bluetoothVibe", temp: true);
				}
				else
				{
					currentScript.deleteFlag("bluetoothVibe", temp: true);
				}
			}
			if (ona == null)
			{
				ona = bt.getOneOna();
				if (ona != null)
				{
					currentScript.setFlag("bluetoothOna", temp: true);
				}
				else
				{
					currentScript.deleteFlag("bluetoothOna", temp: true);
				}
			}
		}
		double num = bpm / maxBpm;
		if (currentState == "edgeHold")
		{
			num /= 2.0;
		}
		if (getTFlag("plug"))
		{
			switch (random.Next(3))
			{
			case 0:
				setPlugSpeed(num);
				break;
			case 1:
				buildExponentialPlug(num, 0.0);
				break;
			case 2:
				buildPulsePlug(num);
				break;
			}
		}
		else if (plug != null)
		{
			sendAllTypes(plug, 0.0);
		}
		if (getTFlag("vibe"))
		{
			switch (random.Next(3))
			{
			case 0:
				setWandSpeed(num);
				break;
			case 1:
				buildExponentialWand(num, 0.0);
				break;
			case 2:
				buildPulseWand(num);
				break;
			}
		}
		else if (wand != null)
		{
			sendAllTypes(wand, 0.0);
		}
		if (ona != null && stroking && getTFlag("ona"))
		{
			if (random.Next(1) == 0)
			{
				setOnaSpeed(num);
			}
		}
		else if (ona != null)
		{
			sendAllTypes(ona, 0.0);
		}
		if (isMain)
		{
			Task.Delay(4000).ContinueWith(delegate
			{
				controlBt(isMain);
			});
		}
	}

	private async void stroker(double position)
	{
		uint num = 1000u;
		if (!stopOna && stroking)
		{
			num = (uint)(60f / bpm / 2f * 1500f);
			if (currentState == "edgeHold")
			{
				num *= 2;
			}
			if (ona != null && ona.LinearAttributes.Any())
			{
				await ona.LinearAsync(num, position);
			}
			if (plug != null && plug.LinearAttributes.Any())
			{
				await plug.LinearAsync(num, position);
			}
			if (wand != null && wand.LinearAttributes.Any())
			{
				await wand.LinearAsync(num, position);
			}
			if (position == 1.0)
			{
				position = 0.0;
			}
			else if (position == 0.0)
			{
				position = 1.0;
			}
		}
		await Task.Delay((int)num);
		stroker(position);
	}

	private void sendAllTypes(ButtplugClientDevice device, double strength)
	{
		if (device == null)
		{
			return;
		}
		if (strength == 0.0)
		{
			stopOna = true;
			device.Stop();
			return;
		}
		stopOna = false;
		if (device.OscillateAttributes.Any())
		{
			device.OscillateAsync(strength);
		}
		if (device.RotateAttributes.Any())
		{
			device.RotateAsync(strength, random.Next(2) == 1);
		}
		if (device.VibrateAttributes.Any())
		{
			device.VibrateAsync(strength);
		}
	}

	private void btLinear(ButtplugClientDevice device, float bpm)
	{
		Task.Run(delegate
		{
			uint num = (uint)(60f / bpm / 2f * 1000f);
			List<LinearCmd.VectorCommand> list = new List<LinearCmd.VectorCommand>();
			if (subliminal)
			{
				num = 2 * num;
			}
			LinearCmd.VectorCommand item = new LinearCmd.VectorCommand(num, 0u);
			LinearCmd.VectorCommand item2 = new LinearCmd.VectorCommand(num, 1u);
			while ((list.Count() * num < 10000 && num != 0) || list.Count() == 0)
			{
				list.Add(item);
				list.Add(item2);
			}
			device.LinearAsync(list);
		});
	}

	private void setPlugSpeed(double speed)
	{
		if ((plug != null && !getTFlag("plug")) || speed == 0.0)
		{
			sendAllTypes(plug, 0.0);
		}
		else if (plug != null)
		{
			if (speed > 1.0)
			{
				speed = 1.0;
			}
			if (speed < 0.05)
			{
				speed = 0.05;
			}
			plugSpeed = speed;
			sendAllTypes(plug, plugSpeed);
		}
	}

	private void setWandSpeed(double speed)
	{
		if ((wand != null && !getTFlag("vibe")) || speed == 0.0)
		{
			sendAllTypes(wand, 0.0);
		}
		else if (wand != null)
		{
			if (speed > 1.0)
			{
				speed = 1.0;
			}
			if (speed < 0.05)
			{
				speed = 0.05;
			}
			wandSpeed = speed;
			sendAllTypes(wand, wandSpeed);
		}
	}

	private void setOnaSpeed(double speed)
	{
		if ((ona != null && !getTFlag("ona")) || speed == 0.0 || !stroking)
		{
			sendAllTypes(ona, 0.0);
		}
		else if (ona != null)
		{
			if (speed > 1.0)
			{
				speed = 1.0;
			}
			if (speed < 0.05)
			{
				speed = 0.05;
			}
			onaSpeed = speed;
			sendAllTypes(ona, onaSpeed);
		}
	}

	private async void buildExponentialPlug(double strength, double oldValue, double additive = 0.0)
	{
		oldValue += additive;
		additive += 0.004;
		setPlugSpeed(oldValue);
		if (!(oldValue >= 1.0 * strength))
		{
			await Task.Delay(500);
			buildExponentialPlug(strength, oldValue, additive);
		}
	}

	private async void buildPulsePlug(double strength, int step = 0, int time = 0)
	{
		double d = 1.0 / strength;
		if (step % 2 == 0)
		{
			setPlugSpeed(0.0);
			Thread.Sleep((int)(30.0 * d));
			setPlugSpeed(0.2 * strength);
			Thread.Sleep((int)(30.0 * d));
			setPlugSpeed(0.4 * strength);
			time += (int)(60.0 * d);
		}
		else
		{
			setPlugSpeed(1.0 * strength);
		}
		if (!((double)time + 100.0 * d > 10000.0))
		{
			await Task.Delay((int)(100.0 * d));
			buildPulsePlug(strength, step + 1, time + (int)(100.0 * d));
		}
	}

	private async void buildExponentialWand(double strength, double oldValue, double additive = 0.0)
	{
		oldValue += additive;
		additive += 0.004;
		setWandSpeed(oldValue);
		if (!(oldValue >= 1.0 * strength))
		{
			await Task.Delay(500);
			buildExponentialWand(strength, oldValue, additive);
		}
	}

	private async void buildPulseWand(double strength, int step = 0, int time = 0)
	{
		double d = 1.0 / strength;
		if (step % 2 == 0)
		{
			setWandSpeed(0.0);
			Thread.Sleep((int)(30.0 * d));
			setWandSpeed(0.2 * strength);
			Thread.Sleep((int)(30.0 * d));
			setWandSpeed(0.4 * strength);
			time += (int)(60.0 * d);
		}
		else
		{
			setWandSpeed(1.0 * strength);
		}
		if (!((double)time + 100.0 * d > 10000.0))
		{
			await Task.Delay((int)(100.0 * d));
			buildPulseWand(strength, step + 1, time + (int)(100.0 * d));
		}
	}

	private string[] searchSubFolders(string currentDirectory)
	{
		List<string> list = new List<string>();
		string[] directories = Directory.GetDirectories(currentDirectory);
		for (int i = 0; i < directories.Length; i++)
		{
			list.AddRange(searchSubFolders(directories[i]));
		}
		list.AddRange(Directory.GetFiles(currentDirectory));
		return list.ToArray();
	}

	public void loadVideoPaths()
	{
		secWindow.videoPaths = Shuffle(mediaCatalog.GetActiveVideoPaths());
	}

	public void pauseSession()
	{
		sessionPaused = true;
		sessionTimer.Stop();
		boredByImages.Stop();
		secWindow.videoWindow.Source = null;
	}

	public void resumeSession()
	{
		if (currentScript != null)
		{
			sessionPaused = false;
			sessionTimer.Start();
			boredByImages.Start();
		}
	}

	public void setObjects(Page1 p)
	{
		optionsScreen(p);
		bt = p.bt;
		if (!controlBtLoopStarted)
		{
			controlBtLoopStarted = true;
			Task.Run(delegate
			{
				controlBt(isMain: true);
			});
		}
		if (!strokerLoopStarted)
		{
			strokerLoopStarted = true;
			Task.Run(delegate
			{
				stroker(0.0);
			});
		}
		mood = int.Parse(getVar("mood", "50"));
		changeMoodBy(0);
		string[] files = Directory.GetFiles(RuntimePaths.TempFlagsDir);
		for (int num = 0; num < files.Length; num++)
		{
			if (files[num].EndsWith("leftEarly.txt"))
			{
				if (getFlagTime("failedSessionEnd") / 60.0 < 14.0)
				{
					leftEarly = false;
					lostConnection = true;
				}
				else
				{
					leftEarly = true;
					lostConnection = false;
				}
				break;
			}
		}
		if (leftEarly)
		{
			p.removeTemp();
		}
	}

	public void ClearSessionRecoveryRuntimeState()
	{
		leftEarly = false;
		lostConnection = false;
	}

	public void safeWord()
	{
		hasEdged++;
		currentState = "module";
		currentScript = new SafeWord(this);
	}

	public void startSession()
	{
		SessionTraceLogger.Reset("session start");
		SessionTraceLogger.Memory("session", "startSession begin");
		sessionLogTimer.Restart();
		performanceLogTimer.Restart();
		try
		{
			using Process process = Process.GetCurrentProcess();
			lastPerformanceCpuTime = process.TotalProcessorTime;
		}
		catch
		{
			lastPerformanceCpuTime = TimeSpan.Zero;
		}
		lastPerformanceSampleUtc = DateTime.UtcNow;
		voc = new Voc(this);
		if (getFlag("break"))
		{
			if (getFlagTimeDays("break") < 8)
			{
				currentScript = new Break(this);
				currentScript.setFlag("closeWithoutSession", temp: true);
			}
			else
			{
				currentScript = new BackFromBreak(this);
			}
		}
		else if (!getFlag("factualFirst"))
		{
			currentScript = new FactualFirst(this);
			linked = false;
		}
		else if (lostConnection)
		{
			currentScript = new LostConnection(this);
			linked = false;
		}
		else if (leftEarly && getFlagTimeDays("failedSessionEnd") == 0)
		{
			currentScript = new LeftEarlyToday(this);
		}
		else if (leftEarly)
		{
			currentScript = new LeftEarly(this);
		}
		else if (getFlagTimeDays("failedSessionEnd") >= 1)
		{
			currentScript = new Reporting(this);
			linked = true;
		}
		else if (getFlagTimeDays("failedSessionEnd") <= 0)
		{
			currentScript = new NoSession(this);
			currentScript.setFlag("closeWithoutSession", temp: true);
		}
		reloadImagesVideos();
		if (!getTFlag("closeWithoutSession"))
		{
			currentScript.setFlag("leftEarly", temp: true);
			settingsRegistry.ExpireStaleCuckFridayCooldown();
			if (isSettingEnabled("wearingChastity"))
			{
				int num = int.Parse(getVar("chastityTime"));
				try
				{
					DateTime dateTime2 = DateTime.Parse(File.ReadAllText(RuntimePaths.Flag("chastityDate"))).AddDays(num);
					if (DateTime.Now > dateTime2)
					{
						currentScript.setFlag("chastityDate", temp: true);
					}
				}
				catch
				{
				}
			}
		}
		secWindow.chromaText = new WordWallText(voc).getVocabList();
		reloadImagesVideos();
		watch.Start();
		boredByImages.Start();
		linkStopWatch.Start();
		censorCheck();
		sessionEndModHookCompleted = false;
		sessionActive = true;
		base.Dispatcher.Invoke(delegate
		{
			totalCurrency.Text = getFavor().ToString() ?? "";
		});
		secWindow.censorText = new WordWallText(voc).getVocabList();
		secWindow.loadSecondWindowForUse();
		if (secWindow.videoPaths.Count > 4)
		{
			currentScript.setFlag("hasVideo", temp: true);
		}
		currentScript = new DebugIntro(this, currentScript);
		secWindow.imageTagger = imageTagger;
		if (getFlag("shortSession"))
		{
			sessionLength = random.Next(600, 900);
			currentScript.deleteFlag("shortSession");
		}
		else if (getFlag("longSession"))
		{
			sessionLength = random.Next(3900, 5700);
			currentScript.deleteFlag("longSession");
		}
		else
		{
			sessionLength = random.Next(900, 2700);
		}
		if (getTFlag("leftEarlyToday"))
		{
			sessionLength = 210;
		}
		if (getFlag("sessionLengthMod"))
		{
			double num2 = getSettingValue("sessionLengthMod");
			if (num2 < -7.0)
			{
				num2 = -8.0;
			}
			sessionLength = (int)((num2 / 10.0 + 1.0) * (double)sessionLength);
		}
		if (!lostConnection && !getTFlag("closeWithoutSession"))
		{
			sessionLength = int.Parse(getVar("sessionLength", "600")) + sessionLength;
			setVar("sessionLength", (sessionLength + int.Parse(getVar("extraTime"))).ToString() ?? "");
			if (sessionLength > 7200)
			{
				sessionLength = 7200;
			}
			setVar("extraTime", "0");
			setVar("mood", (int.Parse(getVar("mood", "10")) / 2 + 25).ToString() ?? "");
		}
		else if (lostConnection)
		{
			sessionLength = int.Parse(getVar("sessionLength", "600"));
			if (sessionLength > 7200)
			{
				sessionLength = 7200;
			}
			else if (sessionLength < 300)
			{
				sessionLength = 300;
			}
		}
		if (getVar("strokeAmount") != "")
		{
			strokeAmount += int.Parse(getVar("strokeAmount"));
		}
		if (getVar("edgesDone") != "")
		{
			edgesDone += int.Parse(getVar("edgesDone"));
		}
		if (getVar("totalTimeOnEdge") != "")
		{
			totalTimeOnEdge += int.Parse(getVar("totalTimeOnEdge"));
		}
		setVar("sessionLength", sessionLength.ToString() ?? "");
		if (Enumerable.Contains(displayOptions, 4) && Enumerable.Contains(displayOptions, 5))
		{
			setNewMediaFormat(random.Next(4, 6));
		}
		else if (Enumerable.Contains(displayOptions, 5))
		{
			setNewMediaFormat(5);
		}
		else
		{
			setNewMediaFormat(4);
		}
		SessionTraceLogger.Info("session", "sessionLength=" + sessionLength + " currentScript=" + currentScript.GetType().Name + " mediaScreen=" + secWindow.getCurrentMediaScreen());
		SessionTraceLogger.Memory("session", "startSession ready");
		if (!strokeLoopStarted)
		{
			strokeLoopStarted = true;
			Task.Run((Action)Stroke);
		}
		Talking(isMain: true);
	}

	private void createBallPit()
	{
		base.Dispatcher.Invoke(delegate
		{
			for (int i = 0; i < ballPitLeft.Length * 2; i++)
			{
				Ellipse ellipse = new Ellipse
				{
					Width = 32.0,
					Height = 32.0,
					Fill = Brushes.White,
					Visibility = Visibility.Hidden,
					IsHitTestVisible = false
				};
				beatCanvas.Children.Add(ellipse);
				if (i % 2 == 0)
				{
					ballPitLeft[i / 2] = ellipse;
					Canvas.SetLeft(ellipse, -32.0);
				}
				else
				{
					ballPitRight[i / 2] = ellipse;
					Canvas.SetRight(ellipse, -32.0);
				}
			}
		}, DispatcherPriority.SystemIdle);
	}

	public void setLOB(bool chkStartUp = true)
	{
		RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
		if (chkStartUp)
		{
			registryKey.SetValue("OpenEdge", System.IO.Path.Combine(RuntimePaths.RuntimeRoot, "LOB", "LOB.exe"));
		}
		else
		{
			registryKey.DeleteValue("OpenEdge", throwOnMissingValue: false);
		}
	}

	private void moveBall(double pos, double additive, int screenWidth, int ballNum)
	{
		pos += additive;
		if (pos < (double)screenWidth)
		{
			base.Dispatcher.Invoke(delegate
			{
				Canvas.SetLeft(ballPitLeft[ballNum], pos);
				Canvas.SetRight(ballPitRight[ballNum], pos);
			});
			Thread.Sleep(10);
			moveBall(pos, additive, screenWidth, ballNum);
		}
		else
		{
			base.Dispatcher.Invoke(delegate
			{
				Canvas.SetLeft(ballPitLeft[ballNum], -32.0);
				Canvas.SetRight(ballPitRight[ballNum], -32.0);
			});
		}
	}

	public void showFavor()
	{
		hideBeatbar(hide: false);
		base.Dispatcher.Invoke(delegate
		{
			DoubleAnimation animation = new DoubleAnimation(totalCurrency.Opacity, 1.0, TimeSpan.FromMilliseconds(2000.0));
			totalCurrency.BeginAnimation(UIElement.OpacityProperty, animation);
		});
	}

	public void hideFavor()
	{
		base.Dispatcher.Invoke(delegate
		{
			DoubleAnimation animation = new DoubleAnimation(totalCurrency.Opacity, 0.0, TimeSpan.FromMilliseconds(4000.0));
			totalCurrency.BeginAnimation(UIElement.OpacityProperty, animation);
		});
	}

	public void setNewTask(int state = 0, int length = -1, int type = -1)
	{
		p.createNewTask(state, length, type);
	}

	private void setBallInMotion()
	{
		if (base.ActualWidth != 0.0)
		{
			double num = base.ActualWidth / 2.0;
			double num2 = (num + 16.0) / (double)(timeBeforeBeat / 10);
			base.Dispatcher.InvokeAsync(delegate
			{
				ballPitLeft[ballPitCount % ballPitRight.Length].Visibility = Visibility.Visible;
				ballPitRight[ballPitCount % ballPitRight.Length].Visibility = Visibility.Visible;
			});
			moveBall(0.0 - num2 - 16.0, num2, (int)num - 16, ballPitCount % ballPitRight.Length);
		}
	}

	public void sortImages()
	{
		secWindow.imageLandscapePaths = new List<string>();
		secWindow.imagePortraitPaths = new List<string>();
		List<string> range = imagePaths;
		if (imagePaths.Count > 1200)
		{
			int maxValue = random.Next(imagePaths.Count - 1200);
			range = imagePaths.GetRange(random.Next(maxValue), 1200);
		}
		secWindow.imageCaptionPaths = imageTagger.getImagesWithTag(new string[1] { "Caption" }, new string[0]);
		foreach (string item in range)
		{
			if (item != "" && !secWindow.imageCaptionPaths.Contains(item))
			{
				string resolvedItem = RuntimePaths.ResolveRuntimePath(item);
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = new FileStream(resolvedItem, FileMode.Open, FileAccess.Read, FileShare.Read);
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				if ((double)bitmapImage.PixelWidth / (double)bitmapImage.PixelHeight > 1.1)
				{
					secWindow.imageLandscapePaths.Add(item);
				}
				else
				{
					secWindow.imagePortraitPaths.Add(item);
				}
			}
		}
		int count = secWindow.imageLandscapePaths.Count;
		int count2 = secWindow.imagePortraitPaths.Count;
		int count3 = secWindow.videoPaths.Count;
		int count4 = secWindow.gifPaths.Count;
		double num = (double)count / (double)(count + count2 + count3 + count4) * 10.0;
		double num2 = (double)count2 / (double)(count + count2 + count3 + count4) * 10.0;
		double num3 = (double)count3 / (double)(count + count2 + count3 + count4) * 10.0;
		double num4 = (double)count4 / (double)(count + count2 + count3 + count4) * 10.0;
		if (secWindow.imagePortraitPaths.Count > 8 && secWindow.imageLandscapePaths.Count > 8)
		{
			int num5 = (int)(num + num2) / 2;
			if (num5 < 1)
			{
				num5 = 1;
			}
			if (num5 > 8)
			{
				num5 = 8;
			}
			for (int i = 0; i < num5; i++)
			{
				displayOptions = displayOptions.Concat(new int[7] { 1, 2, 3, 4, 5, 6, 7 }).ToArray();
			}
		}
		if (secWindow.imagePortraitPaths.Count > 8)
		{
			if (num2 < 1.0)
			{
				num2 = 1.0;
			}
			if (num2 > 8.0)
			{
				num2 = 8.0;
			}
			for (int j = 0; (double)j < num2; j++)
			{
				displayOptions = displayOptions.Concat(new int[4] { 1, 2, 3, 4 }).ToArray();
			}
		}
		if (secWindow.imageLandscapePaths.Count > 8)
		{
			if (num < 1.0)
			{
				num = 1.0;
			}
			if (num > 8.0)
			{
				num = 8.0;
			}
			for (int k = 0; (double)k < num; k++)
			{
				displayOptions = displayOptions.Concat(new int[2] { 5, 6 }).ToArray();
			}
		}
		if (getFlag("noVideo") && getFlagTimeDays("noVideo") >= int.Parse(getVar("noVideoValue")))
		{
			setVar("noVideoValue", "0");
			currentScript.deleteFlag("noVideo");
		}
		if (secWindow.videoPaths.Count > 4 && !getFlag("noVideo"))
		{
			if (num3 > 8.0)
			{
				num3 = 8.0;
			}
			num3 += (double)int.Parse(getVar("videoMod"));
			if (num3 < 1.0)
			{
				num3 = 1.0;
			}
			for (int l = 0; (double)l < num3; l++)
			{
				displayOptions = displayOptions.Concat(new int[1] { 8 }).ToArray();
			}
		}
		if (secWindow.gifPaths.Count > 4 && !getFlag("noVideo"))
		{
			if (num4 < 1.0)
			{
				num4 = 1.0;
			}
			if (num4 > 8.0)
			{
				num4 = 8.0;
			}
			for (int m = 0; (double)m < num4; m++)
			{
				displayOptions = displayOptions.Concat(new int[1] { 9 }).ToArray();
			}
		}
	}

	public void methodPlayASMR(float angle = -1f)
	{
		string name = asmrStrings[random.Next(asmrStrings.Length)];
		SessionTraceLogger.Info("audio", "play ASMR name=" + name + " angle=" + angle);
		if (angle != -1f)
		{
			anglePlayer.setAngle(anglePlayer.numberByName(name), angle);
		}
		anglePlayer.animateSoundVolume(anglePlayer.numberByName(name), (float)(masterVolumeValue / 2.0 * asmrVolumeValue / 2.0));
		anglePlayer.playSound(name);
	}

	public void methodStopASMR()
	{
		SessionTraceLogger.Info("audio", "stop ASMR");
		string[] array = asmrStrings;
		foreach (string name in array)
		{
			anglePlayer.animateSoundVolume(anglePlayer.numberByName(name), 0f);
		}
	}

	public void methodPlayDroningAudio()
	{
		SessionTraceLogger.Info("audio", "play droning");
		anglePlayer.playSound("140HZ");
		anglePlayer.playSound("146HZ");
	}

	public void methodStopDroningAudio()
	{
		SessionTraceLogger.Info("audio", "stop droning");
		anglePlayer.stopSound("140HZ");
		anglePlayer.stopSound("146HZ");
	}

	public void playSnapAudio()
	{
		SessionTraceLogger.Info("audio", "snap");
		anglePlayer.playSound("snap", masterVolumeValue / 2.0 * asmrVolumeValue / 2.0);
	}

	public void playDoubleSnapAudio()
	{
		SessionTraceLogger.Info("audio", "double snap");
		anglePlayer.playSound("doubleSnap", masterVolumeValue / 2.0 * asmrVolumeValue / 2.0);
	}

	private void hideBeatbar(bool hide = true)
	{
		base.Dispatcher.Invoke(delegate
		{
			if (hide)
			{
				for (int i = 0; i < ballPitLeft.Count(); i++)
				{
					ballPitLeft[i].Visibility = Visibility.Hidden;
					ballPitRight[i].Visibility = Visibility.Hidden;
				}
				beatBarBeatRect.Visibility = Visibility.Hidden;
				beatRowEllipse.Visibility = Visibility.Hidden;
				gradBlackTransImg.Visibility = Visibility.Hidden;
				gradBlackTransFlipImg.Visibility = Visibility.Hidden;
			}
			else
			{
				for (int j = 0; j < ballPitLeft.Count(); j++)
				{
					ballPitLeft[j].Visibility = Visibility.Visible;
					ballPitRight[j].Visibility = Visibility.Visible;
				}
				beatBarBeatRect.Visibility = Visibility.Visible;
				beatRowEllipse.Visibility = Visibility.Visible;
				gradBlackTransImg.Visibility = Visibility.Visible;
				gradBlackTransFlipImg.Visibility = Visibility.Visible;
			}
			noBalls = hide;
		});
	}

	private bool[][] createBeatArrays()
	{
		List<string[]> beatStrings = new List<string[]>
		{
			"0".Split('.'),
			"0.0.1".Split('.'),
			"0.1.1.1.0.1.1".Split('.'),
			"0.1.1.1".Split('.'),
			"0.1".Split('.'),
			"0.0.1.1.1".Split('.'),
			"0.0.1.1.1.1.1".Split('.'),
			"0.0.1.1.1.1.1.1.1".Split('.'),
			"1".Split('.')
		};
		return createBeat(beatStrings);
	}

	public bool[][] createBeat(List<string[]> beatStrings)
	{
		bool[][] array = new bool[beatStrings.Count][];
		for (int i = 0; i < beatStrings.Count; i++)
		{
			array[i] = new bool[beatStrings[i].Length];
			for (int j = 0; j < beatStrings[i].Length; j++)
			{
				if (beatStrings[i][j] == "1")
				{
					array[i][j] = true;
				}
				else
				{
					array[i][j] = false;
				}
			}
		}
		return array;
	}

	public void setAsmrVolume(double volume)
	{
		asmrVolumeValue = volume;
		anglePlayer.asmrVolume = asmrVolumeValue / 2.0 * masterVolumeValue / 2.0;
	}

	public void setMasterVolume(double volume)
	{
		masterVolumeValue = volume;
		anglePlayer.asmrVolume = asmrVolumeValue / 2.0 * masterVolumeValue / 2.0;
	}

	public void methodHypnosisOn()
	{
		SessionTraceLogger.Info("hypnosis", "on requested currentState=" + currentState + " script=" + currentScript?.GetType().Name);
		SessionTraceLogger.Memory("hypnosis", "before on");
		bool startStrobeLoop = false;
		int strobeGeneration;
		lock (strobeTextLock)
		{
			if (strobeTextRunning)
			{
				SessionTraceLogger.Info("hypnosis-strobe", "duplicate HYPNOSISON ignored; generation=" + strobeTextGeneration);
			}
			else
			{
				strobeTextRunning = true;
				strobeGeneration = ++strobeTextGeneration;
				startStrobeLoop = true;
			}
			strobeGeneration = strobeTextGeneration;
		}
		base.Dispatcher.Invoke(delegate
		{
			spiral.Visibility = Visibility.Visible;
			hypnoOverlay.Visibility = Visibility.Visible;
			textCenter.Text = "";
			anglePlayer.setDroningVolume();
			textCenter.Visibility = Visibility.Visible;
			subliminal = true;
			anglePlayer.perpetualIntensity = 1;
			if (isSettingEnabled("asmr"))
			{
				anglePlayer.perpetualEarLicking();
			}
			methodPlayDroningAudio();
			talkingTime = 3000;
			secWindow.censorMode = 0;
			if (wantsBeatBar)
			{
				hideBeatbar();
			}
			if (startStrobeLoop)
			{
				SessionTraceLogger.Info("hypnosis-strobe", "start generation=" + strobeGeneration);
				Task.Run(delegate
				{
					strobeText(0, strobeGeneration);
				});
			}
			methodHypnosisImagesOn();
			boredByImages.Stop();
			foreach (TextBlock child in textPanel.Children)
			{
				setTPOTo0(child);
			}
			if (ona != null || plug != null || wand != null)
			{
				specialButtons("I came", 3);
			}
			secWindow.changeBg("pinkbg", hypnosisOn: true);
		});
	}

	public void methodHypnosisOff()
	{
		SessionTraceLogger.Info("hypnosis", "off requested currentState=" + currentState + " script=" + currentScript?.GetType().Name);
		SessionTraceLogger.Memory("hypnosis", "before off");
		lock (strobeTextLock)
		{
			strobeTextRunning = false;
			strobeTextGeneration++;
			SessionTraceLogger.Info("hypnosis-strobe", "stop requested generation=" + strobeTextGeneration);
		}
		base.Dispatcher.InvokeAsync(delegate
		{
			anglePlayer.stopPerpetualEarlicking = true;
			anglePlayer.stopDroning = true;
			spiral.Visibility = Visibility.Hidden;
			hypnoOverlay.Visibility = Visibility.Hidden;
			textCenter.Visibility = Visibility.Hidden;
			strobeRect.Visibility = Visibility.Hidden;
			talkingTime = 6000;
			hypnoIntensity = 1;
			subliminal = false;
			if (wantsBeatBar)
			{
				hideBeatbar(hide: false);
			}
			textCenter.Text = "";
			methodStopDroningAudio();
			setNewMediaFormat();
			if (!setBackground(p.backgroundImg))
			{
				secWindow.changeBg("heartsbgblack", hypnosisOn: false);
			}
		});
	}

	public void methodHypnosisIntensity(int intensity)
	{
		SessionTraceLogger.Info("hypnosis", "intensity delta=" + intensity + " current=" + hypnoIntensity);
		int num = hypnoIntensity + intensity;
		if (num <= 0)
		{
			num = 1;
		}
		if (num > 7)
		{
			num = 7;
		}
		hypnoIntensity = num;
		setNewSpeed(20 * hypnoIntensity);
		anglePlayer.perpetualIntensity = hypnoIntensity;
	}

	public void methodIllegalCum()
	{
		currentScript = new IllegalCum(this, currentScript);
	}

	public void methodHoldBreath()
	{
		Task.Run((Action)holdBreath);
	}

	public void methodCancelBreath()
	{
		if (holdingBreath)
		{
			cancelBreath = true;
		}
	}

	public void methodOrgasmDenied()
	{
		orgasmDenied = true;
		currentScript.setFlag("orgasmDenied", temp: true);
	}

	public void methodSubliminalAudio(bool active)
	{
		SessionTraceLogger.Info("audio", "subliminal audio active=" + active);
		subliminalAudio = active;
	}

	public void methodSubliminal(bool active)
	{
		SessionTraceLogger.Info("hypnosis", "subliminal visual active=" + active);
		subliminal = active;
	}

	public void methodASMRDirectional(string s)
	{
		s = s.Replace("ASMRON:", "");
		s = s.Trim();
		methodPlayASMR(float.Parse(s));
	}

	public void methodHypnosisImagesOn()
	{
		boredByImages.Stop();
		if (Enumerable.Contains(displayOptions, 5))
		{
			setNewMediaFormat(5);
		}
		else if (Enumerable.Contains(displayOptions, 4))
		{
			setNewMediaFormat(4);
		}
	}

	public void methodStopStroking()
	{
		currentScript.talkLocked = true;
		bool sayStop = stroking;
		stroking = false;
		setNewSpeed(50f);
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			removeSpecialButtons("I'm on the edge", 1);
			if (sayStop && currentState != "anal")
			{
				setTPText(lr.getVocab("stopStroking"));
			}
			else if (sayStop && currentState == "anal")
			{
				setTPText(lr.getVocab("stopAnal"));
			}
			currentScript.talkLocked = false;
		});
	}

	public void methodPauseStroking()
	{
		currentScript.talkLocked = true;
		stroking = false;
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			setTPText(lr.getVocab("stopStroking"));
			currentScript.talkLocked = false;
		});
	}

	public void methodSpeed(string s)
	{
		s = s.Replace("SPEED:", "");
		s = s.Trim();
		setNewSpeed(int.Parse(s));
		anglePlayer.perpetualIntensity++;
	}

	public void methodSpeedUp()
	{
		currentScript.talkLocked = true;
		setNewSpeed(bpm + (float)random.Next(60, 80), addMod: false);
		stroking = true;
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			setTPText(lr.getVocab("strokingFaster"));
			currentScript.talkLocked = false;
		});
	}

	public void methodSpeedDown()
	{
		currentScript.talkLocked = true;
		setNewSpeed(bpm - (float)random.Next((int)((double)bpm * 0.2), (int)((double)bpm * 0.9)), addMod: false);
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			setTPText(lr.getVocab("strokingSlower"));
			currentScript.talkLocked = false;
		});
	}

	public void methodStrokeSlow()
	{
		currentScript.talkLocked = true;
		edgeAllowed = false;
		stroking = true;
		setNewSpeed(random.Next(10, 40));
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			specialButtons("I'm on the edge", 1);
			setTPText(lr.getVocab("strokingSlowStart"));
			currentScript.talkLocked = false;
		});
	}

	public void methodStrokeOn()
	{
		specialButtons("I'm on the edge", 1);
		edgeAllowed = false;
		stroking = true;
	}

	public void methodStrokeOff()
	{
		stroking = false;
		if (ona == null && plug == null && wand == null)
		{
			removeSpecialButtons("I'm on the edge", 1);
		}
	}

	public void methodStrokeNormal()
	{
		currentScript.talkLocked = true;
		edgeAllowed = false;
		bool sayStroke = stroking;
		stroking = true;
		setNewSpeed(random.Next(40, 80));
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			specialButtons("I'm on the edge", 1);
			if (!sayStroke)
			{
				setTPText(lr.getVocab("strokingStart"));
			}
			currentScript.talkLocked = false;
		});
	}

	public void methodStrokeFast()
	{
		currentScript.talkLocked = true;
		edgeAllowed = false;
		stroking = true;
		setNewSpeed(random.Next(80, 120));
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			specialButtons("I'm on the edge", 1);
			setTPText(lr.getVocab("strokingFastStart"));
			currentScript.talkLocked = false;
		});
	}

	public void methodEdge()
	{
		if (RunModHookBeforeBase("edgeOpportunity"))
		{
			return;
		}
		currentScript.talkLocked = true;
		stroking = true;
		setNewSpeed(maxBpm);
		currentState = "edge";
		edgeAllowed = true;
		if (random.Next(1, 100) > 90 && secWindow.imageCaptionPaths.Count > 10)
		{
			secWindow.showCaptionImage();
		}
		longEdge();
		scriptPaused = true;
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			setTPText(lr.getVocab("edge"));
			currentScript.talkLocked = false;
			specialButtons("I'm on the edge", 1);
			specialButtons("I came", 3);
		});
	}

	public void methodEdgeHold()
	{
		if (RunModHookBeforeBase("edgeOpportunity"))
		{
			return;
		}
		if (!isSettingEnabled("edgeHold"))
		{
			SessionTraceLogger.Info("edgehold", "edgeHold disabled; falling back to normal edge");
			methodEdge();
			return;
		}
		currentScript.talkLocked = true;
		stroking = true;
		setNewSpeed(maxBpm);
		currentState = "edgeHold";
		controlBt();
		edgeAllowed = true;
		if (random.Next(1, 100) > 93 && secWindow.imageCaptionPaths.Count > 10)
		{
			secWindow.showCaptionImage();
		}
		longEdge();
		scriptPaused = true;
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			setTPText(lr.getVocab("edge"));
			currentScript.talkLocked = false;
			specialButtons("I'm on the edge", 1);
			specialButtons("I came", 3);
		});
	}

	public void methodEdgeRelease()
	{
		scriptPaused = true;
		stroking = false;
		edgeAllowed = false;
		sendAllTypes(plug, 0.0);
		sendAllTypes(wand, 0.0);
		sendAllTypes(ona, 0.0);
		stopOna = true;
		setNewSpeed(50f);
		setTPText(lr.getVocab("edgeStop"));
		Task.Delay(2000).ContinueWith(delegate
		{
			setTPText(lr.getVocab("break"));
			Task.Delay(6000).ContinueWith(delegate
			{
				removeSpecialButtons("I came", 3);
				scriptPaused = false;
			});
		});
	}

	public void methodOrgasmDecide()
	{
		methodEdge();
		currentState = "orgasmDecide";
	}

	public void methodCBTStart()
	{
		cbtStopWatch.Restart();
		currentState = "cbt";
		setNewSpeed(random.Next(40, 70), addMod: false);
		stroking = true;
	}

	public void methodCBTExtremeStart()
	{
		cbtStopWatch.Restart();
		currentState = "cbtExtreme";
		setNewSpeed(random.Next(80, 100), addMod: false);
		stroking = true;
	}

	public void methodAnalStart()
	{
		watch.Restart();
		cbtStopWatch.Restart();
		currentState = "anal";
		setNewSpeed(random.Next(20, 40), addMod: false);
		stroking = true;
	}

	public void methodAnalExtremeStart()
	{
		watch.Restart();
		cbtStopWatch.Restart();
		currentState = "analExtreme";
		setNewSpeed(random.Next(30, 40), addMod: false);
		stroking = true;
	}

	public void methodTime(int secondsBeforePass)
	{
		int rand = random.Next(int.MaxValue);
		timed = rand;
		Task.Delay(secondsBeforePass * 1000).ContinueWith(delegate
		{
			if (timed == rand)
			{
				currentScript.talkLocked = false;
				sessionPaused = false;
				scriptPaused = false;
				if (currentScript.repeating)
				{
					currentScript.repeating = false;
					currentScript.location++;
				}
				removeTalkButtons("");
			}
		});
	}

	public void methodSilentStop()
	{
		currentScript.talkLocked = true;
		stroking = false;
		setNewSpeed(50f);
		Task.Delay(timeBeforeBeat).ContinueWith(delegate
		{
			removeSpecialButtons("I'm on the edge", 1);
			currentScript.talkLocked = false;
		});
	}

	public void methodEdgeHoldDuration(string s)
	{
		if (!isSettingEnabled("edgeHold"))
		{
			SessionTraceLogger.Info("edgehold", "timed edgeHold disabled; falling back to normal edge from " + s);
			methodEdge();
			return;
		}
		string[] array = s.Split("EDGEHOLD:");
		try
		{
			specialButtons("I'm on the edge", 1);
			edgeAllowed = true;
			longEdge();
			secondsOfEdging = int.Parse(array[1]);
			methodEdgeHold();
		}
		catch
		{
			Console.Write("couldn't convert the string to an int, the string was: " + array[1]);
		}
	}

	public void methodEdgeMultiple(string s)
	{
		string[] array = s.Split("EDGE:");
		try
		{
			edgeAllowed = true;
			specialButtons("I'm on the edge", 1);
			longEdge();
			int num = int.Parse(array[1]) - 1;
			promissedEdges += num;
			methodEdge();
		}
		catch
		{
			Console.Write("couldn't convert the string to an int, the string was: " + array[1]);
		}
	}

	public void methodTagger()
	{
		base.Dispatcher.Invoke(delegate
		{
			pauseSession();
			imageTagger.setMuted(p.muteVideos);
			imageTagger.setForcedTagAmount(random.Next(40, 60));
			base.NavigationService.Navigate(imageTagger);
		});
	}

	public void setTag(string tag, bool isTemp = false)
	{
		secWindow.tagPaths = imageTagger.allTaggedWith(tag);
		secWindow.useTaggedPaths(isTemp);
	}

	public bool atLeastXMedia(string tagString, int minimumImages = 3)
	{
		return imageTagger.allTaggedWith(tagString).Count >= minimumImages;
	}

	public void unSetTag()
	{
		secWindow.tagPaths = new List<string>();
	}

	public bool atLeastXImages(string tagString, int minimumImages = 3)
	{
		return imageTagger.getTaggedImage(tagString).Count >= minimumImages;
	}

	public bool atLeastXVideos(string tag, int minimumImages = 3)
	{
		return imageTagger.getTaggedVideo(tag).Count >= minimumImages;
	}

	private void anal(bool extreme = false)
	{
		stroking = true;
		double num = 1.0;
		if (extreme)
		{
			num = 1.8;
		}
		if ((double)watch.ElapsedMilliseconds > (double)(15000 + random.NextInt64(40000L)) * num && longSilence >= 1)
		{
			if ((double)(bpm + (float)random.Next((int)(20.0 * num))) < 60.0 * num)
			{
				setNewSpeed(bpm + (float)random.Next(10, 20), addMod: false);
				Task.Delay(timeBeforeBeat).ContinueWith(delegate
				{
					setTPText("speed up");
				});
			}
			else
			{
				setNewSpeed(bpm - (float)random.Next((int)((double)bpm * 0.2), (int)((double)bpm * 0.9)), addMod: false);
				Task.Delay(timeBeforeBeat).ContinueWith(delegate
				{
					setTPText("slow down");
					currentScript.talkLocked = false;
				});
			}
			longSilence = 2;
			watch.Restart();
		}
		else
		{
			longSilence++;
			if (random.Next(2, 8) < longSilence)
			{
				setTPText(currentScript.Talk());
				longSilence = 0;
			}
		}
		if ((double)cbtStopWatch.ElapsedMilliseconds > (double)random.Next(100000, 250000) * num)
		{
			currentScript.repeating = false;
			currentScript.location++;
			Task.Delay(timeBeforeBeat).ContinueWith(delegate
			{
				setNewSpeed(50f);
				stroking = false;
			});
			currentState = "module";
		}
	}

	private void cbtExtreme()
	{
		stroking = true;
		if (random.Next(10) > 7)
		{
			setTPText(currentScript.Talk());
		}
		if (cbtStopWatch.ElapsedMilliseconds > random.Next(60000, 800000))
		{
			currentScript.repeating = false;
			currentScript.location++;
			Task.Delay(timeBeforeBeat).ContinueWith(delegate
			{
				setNewSpeed(50f, addMod: false);
				stroking = false;
			});
			currentState = "module";
		}
	}

	private void cbt()
	{
		stroking = true;
		if (random.Next(10) > 7)
		{
			setTPText(currentScript.Talk());
		}
		if (cbtStopWatch.ElapsedMilliseconds > random.Next(30000, 400000))
		{
			currentScript.repeating = false;
			currentScript.location++;
			Task.Delay(timeBeforeBeat).ContinueWith(delegate
			{
				setNewSpeed(50f, addMod: false);
				stroking = false;
			});
			currentState = "module";
		}
	}

	private void oD()
	{
		if (RunModHookBeforeBase("orgasmDecision"))
		{
			return;
		}
		setVar("sessionLength", 0.ToString() ?? "");
		daysSinceFull = getFlagTimeDays("fullOrgasm");
		difficulty += (double)getSettingValue("scoreMod") / 10.0;
		if (difficulty < 0.5)
		{
			difficulty = 0.5;
		}
		int num = (int)((double)strokeAmount * 0.02 + (double)(daysSinceFull * 6) + (double)edgesDone * 0.2 + (double)totalTimeOnEdge * 0.05 + (double)(int.Parse(getVar("Denied")) * 4) - (double)(100 - mood) * difficulty);
		if (num < 0)
		{
			num = 0;
		}
		int num2 = random.Next(num);
		if (orgasmDenied || getTFlag("orgasmDenied"))
		{
			num2 = 0;
		}
		if (getFlag("debt") && num2 >= 80)
		{
			currentScript.deleteFlag("debt");
			setVar("strokeAmount", "0");
			strokeAmount = 0;
			setVar("edgesDone", "0");
			edgesDone = 0;
			setVar("totalTimeOnEdge", "0");
			totalTimeOnEdge = 0;
			currentScript = new OrgasmDecideDebt(this, currentScript);
		}
		if (getFlag("note"))
		{
			if (num2 > 100)
			{
				currentState = "module";
				currentScript = new OrgasmDecideNote(this, currentScript);
				return;
			}
			num2 = 1;
		}
		if (num2 >= 100)
		{
			if (num2 >= 120)
			{
				methodCumming();
				currentScript = new OrgasmDecideCum(this, currentScript);
			}
			else
			{
				currentState = "module";
				currentScript = new OrgasmDecideConditional(this, currentScript);
			}
		}
		else if (num2 >= 80)
		{
			methodRuin();
			currentScript = new OrgasmDecideRuin(this, currentScript);
		}
		else if (getFavor() > 0 && num2 >= 40 && !orgasmDenied && isSettingEnabled("taskScreen") && isSettingEnabled("findom"))
		{
			showFavor();
			currentState = "module";
			currentScript = new OrgasmDecideFavor(this, currentScript);
		}
		else
		{
			methodDenial();
			currentState = "module";
			currentScript = new OrgasmDecideDeny(this, currentScript);
		}
	}

	private float strokeMod()
	{
		double num = 1.0;
		if (currentScript != null)
		{
			int num2 = int.Parse(currentScript.getVar("strokeMod"));
			if (num2 > 0)
			{
				num = 1 + num2 / 10;
			}
			if (num2 < 0)
			{
				num2 *= -1;
				for (int i = 0; i < num2; i++)
				{
					num *= 0.9;
				}
			}
		}
		return (float)num;
	}

	public void methodCumming()
	{
		setVar("fullOrgasm", "0");
		setVar("denied", "0");
		stroking = true;
		setNewSpeed(maxBpm);
		scriptPaused = true;
		setTPText(lr.getVocab("cum"));
		cumButtons();
		currentState = "cum";
		setVar("strokeAmount", "0");
		strokeAmount = 0;
		setVar("edgesDone", "0");
		edgesDone = 0;
		setVar("totalTimeOnEdge", "0");
		totalTimeOnEdge = 0;
	}

	public void methodDenial()
	{
		currentScript.addVar("denied,1");
		stroking = false;
		setTPText(lr.getVocab("deny"));
	}

	public bool ApplyModOutcome(string kind, string key)
	{
		kind = (kind ?? "").Trim();
		key = (key ?? "").Trim();
		ModOutcomeDefinition outcome = ModService.GetEnabledOutcomeDefinitions().FirstOrDefault(delegate(ModOutcomeDefinition candidate)
		{
			return string.Equals(candidate.Kind, kind, StringComparison.OrdinalIgnoreCase) && string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase);
		});
		if (outcome == null)
		{
			SessionTraceLogger.Info("mod-outcome", "unknown outcome kind=" + kind + " key=" + key);
			return false;
		}
		if (isSettingEnabled("wearingChastity") && !outcome.AllowedWhileChaste)
		{
			SessionTraceLogger.Info("mod-outcome", "skipped restricted outcome while chaste kind=" + kind + " key=" + key);
			return false;
		}
		SessionTraceLogger.Info("mod-outcome", "apply kind=" + kind + " key=" + key + " label=" + outcome.Label);
		if (outcome.CountsAsEdge)
		{
			edgesDone++;
			setVar("edgesDone", edgesDone.ToString() ?? "");
		}
		if (outcome.CountsAsFullOrgasm)
		{
			setVar("fullOrgasm", "0");
		}
		if (outcome.CountsAsRuinedOrgasm)
		{
			setPersistentFlag("ruinedOrgasm");
		}
		if (outcome.ResetsDeniedCounter)
		{
			setVar("denied", "0");
		}
		if (outcome.IncrementsDeniedCounter)
		{
			string value = getVar("denied");
			int currentDenied = int.TryParse(value, out int parsedDenied) ? parsedDenied : 0;
			setVar("denied", (currentDenied + 1).ToString() ?? "");
		}
		if (outcome.ResetsEdgeCounters)
		{
			setVar("strokeAmount", "0");
			strokeAmount = 0;
			setVar("edgesDone", "0");
			edgesDone = 0;
			setVar("totalTimeOnEdge", "0");
			totalTimeOnEdge = 0;
		}
		if (!string.IsNullOrWhiteSpace(outcome.State))
		{
			currentState = outcome.State.Trim();
		}
		MarkCurrentModHookHandled("outcome=" + kind + ":" + key);
		return true;
	}

	public void methodRuin()
	{
		setVar("denied", "0");
		setNewSpeed(maxBpm);
		stroking = true;
		scriptPaused = true;
		setTPText(lr.getVocab("ruin"));
		cumButtons();
		currentState = "ruin";
		setVar("strokeAmount", (strokeAmount / 2).ToString() ?? "");
		strokeAmount /= 2;
		setVar("edgesDone", (edgesDone / 2).ToString() ?? "");
		edgesDone /= 2;
		setVar("totalTimeOnEdge", (totalTimeOnEdge / 2).ToString() ?? "");
		totalTimeOnEdge /= 2;
	}

	private void strokeLink()
	{
		setVar("state", "-1");
		specialButtons("I'm on the edge", 1);
		specialButtons("Please...", 6);
		if (!isSettingEnabled("wearingChastity"))
		{
			edgeAllowed = false;
			linkStopWatch.Restart();
			watch.Restart();
			int num = random.Next(30, 120);
			setNewSpeed(num);
			if (!stroking)
			{
				string text = ((num < 50) ? lr.getVocab("strokingSlowStart") : ((num <= 100) ? lr.getVocab("strokingStart") : lr.getVocab("strokingFastStart")));
				if (getTFlag("ona"))
				{
					setTPText(text);
				}
				else
				{
					setTPText(text + "\n" + strokeMethod());
				}
			}
			stroking = true;
			setVar("state", "0");
			currentState = "stroke";
		}
		else
		{
			linkStopWatch.Restart();
			watch.Restart();
			if (isSettingEnabled("vibrator") || getTFlag("vibe"))
			{
				currentState = "vibe";
			}
		}
	}

	private void holdBreath()
	{
		if (!holdingBreath)
		{
			int time = getSettingValue("breathTime", 60);
			holdingBreath = true;
			setTPText(lr.getVocab("breathStart"));
			switch (random.Next(4))
			{
			case 0:
				breathLongHold(time);
				break;
			case 1:
				breathExhaleHold(time);
				break;
			case 2:
				breathHyperventHold(time);
				break;
			case 3:
				breathStairs(time);
				break;
			}
		}
	}

	private async void breathLongHold(int time)
	{
		setBreathText("Take a deep Breath");
		string prevState = currentState;
		currentState = "breathHold";
		await Task.Delay(5000);
		currentState = "breathHold";
		setBreathText("Hold");
		specialButtons("I can't hold my breath", 2);
		breathTalk(random.Next(time / 2, time), prevState);
	}

	private async void breathExhaleHold(int time)
	{
		setBreathText("Fully exhale");
		string prevState = currentState;
		await Task.Delay(5000);
		currentState = "breathHold";
		specialButtons("I can't hold my breath", 2);
		setBreathText("Hold");
		breathTalk(random.Next(time / 4, (int)((float)time / 2.5f)), prevState);
	}

	private void breathHyperventHold(int time)
	{
		setBreathText("Hyperventilate");
		string prevState = currentState;
		Thread.Sleep(30000);
		setBreathText("Take a deep Breath");
		Thread.Sleep(5000);
		currentState = "breathHold";
		specialButtons("I can't hold my breath", 2);
		setBreathText("Hold");
		breathTalk(random.Next(time, (int)((double)time * 1.3)), prevState);
	}

	private async void breathStairs(int time)
	{
		setBreathText("Take a deep Breath");
		string prevState = currentState;
		float thisRand = random.Next(3, 6);
		for (int i = 0; (float)i < thisRand; i++)
		{
			await Task.Delay(5000);
			currentState = "breathHold";
			specialButtons("I can't hold my breath", 2);
			setBreathText("Hold");
			breathTalk(random.Next(time / 3, time / 2), prevState, backToOldState: false);
			if (thisRand >= (float)(i + 1))
			{
				setBreathText("Take one breath");
			}
		}
		breathEnd(prevState);
	}

	private void setBreathText(string text)
	{
		base.Dispatcher.InvokeAsync(() => textBreathHold.Text = text, DispatcherPriority.SystemIdle);
	}

	private async void breathEnd(string oldState)
	{
		if (!cancelBreath)
		{
			setTPText(lr.getVocab("breathEnd"));
			changeMoodBy(2);
		}
		removeSpecialButtons("I can't hold my breath", 2);
		setBreathText("Release");
		await Task.Delay(5000);
		setBreathText("");
		holdingBreath = false;
		cancelBreath = false;
		currentState = oldState;
	}

	private void breathTalk(int holdLength, string prevState, bool backToOldState = true)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		cancelBreath = false;
		while (stopwatch.Elapsed.TotalSeconds < (double)holdLength && !cancelBreath)
		{
			Task.Delay(2000).Wait();
			setVar("breathHoldProgress", (stopwatch.Elapsed.TotalSeconds / (double)holdLength).ToString() ?? "");
		}
		if (backToOldState)
		{
			breathEnd(prevState);
		}
	}

	private void holdBreathLoad()
	{
		TransformGroup transformGroup = new TransformGroup();
		transformGroup.Children.Add(new ScaleTransform(1.0, 1.0));
		textBreathHold.RenderTransform = transformGroup;
		textBreathHold.RenderTransformOrigin = new Point(0.5, 0.5);
		DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 1.2, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation.AutoReverse = true;
		doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation2 = new DoubleAnimation(1.0, 1.2, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation2.AutoReverse = true;
		doubleAnimation2.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.2, 1.0, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation3.AutoReverse = true;
		ExponentialEase exponentialEase = new ExponentialEase();
		exponentialEase.EasingMode = EasingMode.EaseOut;
		doubleAnimation3.RepeatBehavior = RepeatBehavior.Forever;
		doubleAnimation3.EasingFunction = exponentialEase;
		ColorAnimation colorAnimation = new ColorAnimation();
		colorAnimation.AutoReverse = true;
		colorAnimation.From = Colors.White;
		colorAnimation.To = Color.FromRgb(byte.MaxValue, 0, byte.MaxValue);
		colorAnimation.Duration = new Duration(TimeSpan.FromSeconds(1.5));
		colorAnimation.RepeatBehavior = RepeatBehavior.Forever;
		colorAnimation.EasingFunction = exponentialEase;
		Storyboard.SetTargetName(doubleAnimation, textBreathHold.Name);
		Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
		holdBreathStoryBoard.Children.Add(doubleAnimation);
		Storyboard.SetTargetName(doubleAnimation2, textBreathHold.Name);
		Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
		holdBreathStoryBoard.Children.Add(doubleAnimation2);
		Storyboard.SetTargetName(doubleAnimation3, textBreathHold.Name);
		Storyboard.SetTargetProperty(doubleAnimation3, new PropertyPath(UIElement.OpacityProperty));
		holdBreathStoryBoard.Children.Add(doubleAnimation3);
		Storyboard.SetTargetName(colorAnimation, textBreathHold.Name);
		Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("(TextBlock.Foreground).(SolidColorBrush.Color)"));
		holdBreathStoryBoard.Children.Add(colorAnimation);
		holdBreathStoryBoard.Begin(textBreathHold);
		holdBreathStoryBoard.SetSpeedRatio(0.1);
	}

	public void setNewMediaFormat(int mediaFormat = 0)
	{
		secWindow.mediasCounter = 0;
		if (lastMediaWasVideo)
		{
			mediaFormat = ((Enumerable.Contains(displayOptions, 4) && Enumerable.Contains(displayOptions, 5)) ? random.Next(4, 6) : ((!Enumerable.Contains(displayOptions, 4)) ? 5 : 4));
			lastMediaWasVideo = false;
			secWindow.mediaScreen(mediaFormat);
			secWindow.mediaScreen();
			return;
		}
		if (mediaFormat == 0)
		{
			if (displayOptions == null || displayOptions.Length == 0)
			{
				mediaFormat = 4;
			}
			else
			{
				for (int attempts = 0; attempts < 20 && (mediaFormat == secWindow.getCurrentMediaScreen() || mediaFormat == 0 || (mediaFormat == 10 && !linkStopWatch.IsRunning)); attempts++)
				{
					mediaFormat = displayOptions[random.Next(displayOptions.Length)];
				}
				if (mediaFormat == 0 || (mediaFormat == 10 && !linkStopWatch.IsRunning))
				{
					mediaFormat = displayOptions.FirstOrDefault(delegate(int option)
					{
						return option != 0 && (option != 10 || linkStopWatch.IsRunning);
					});
					if (mediaFormat == 0)
					{
						mediaFormat = 4;
					}
				}
			}
		}
		secWindow.mediaScreen(mediaFormat);
		secWindow.mediaScreen();
		boredByImages.Restart();
	}

	private void setNewMedia()
	{
		secWindow.mediaScreen();
	}

	private void setNewSpeed(float speed, bool addMod = true)
	{
		if (speed <= 0f)
		{
			speed = 1f;
		}
		float num = speed;
		if (addMod)
		{
			num = speed * strokeMod();
		}
		if (num > maxBpm)
		{
			bpm = maxBpm;
		}
		else if (num < minBpm)
		{
			bpm = minBpm;
		}
		else
		{
			bpm = num;
		}
		setVar("bpm", bpm.ToString() ?? "");
		base.Dispatcher.InvokeAsync(delegate
		{
			spiralStoryBoard.SetSpeedRatio(spiral, bpm / 60f);
			hypnosisOverlayStoryboard.SetSpeedRatio(hypnoOverlay, bpm / 60f);
		}, DispatcherPriority.SystemIdle);
	}

	private void chastityTaunt()
	{
		if (linkStopWatch.ElapsedMilliseconds < 20000 + random.NextInt64(120000L))
		{
			if (watch.ElapsedMilliseconds > 8000 + random.NextInt64(12000L))
			{
				if (random.Next(0, 100) >= 70 && isSettingEnabled("breathPlay"))
				{
					Task.Run((Action)holdBreath);
				}
				longSilence = 1;
				watch.Reset();
			}
			else
			{
				longSilence++;
				if (random.Next(2, 4) < longSilence)
				{
					setTPText(voc.getVoc("tease"));
					longSilence = 0;
				}
			}
		}
		else if (!holdingBreath && secWindow.getCurrentMediaScreen() != 10)
		{
			longSilence = 0;
			linkStopWatch.Restart();
			pickScript();
		}
	}

	private void StrokeTaunting()
	{
		if (linkStopWatch.ElapsedMilliseconds < 50000 + random.NextInt64(120000L))
		{
			if (watch.ElapsedMilliseconds > 14000 + random.NextInt64(120000L))
			{
				if (random.Next(0, 100) >= 82 && isSettingEnabled("breathPlay"))
				{
					Task.Run((Action)holdBreath);
				}
				if (sessionTimer.Elapsed.TotalMinutes > 4.0 && bpm > 100f * strokeMod())
				{
					if (random.Next(10) < 7)
					{
						methodEdge();
					}
					else
					{
						methodEdgeHold();
					}
					if (isSettingEnabled("treats") && !getTFlag("gag") && mood > 60 && random.Next(100) > 96)
					{
						setTPText("you may take a treat when you reach the edge");
					}
				}
				else if (bpm + (float)random.Next(300) < 260f * strokeMod())
				{
					methodSpeedUp();
				}
				else
				{
					methodSpeedDown();
				}
				longSilence = 1;
				watch.Reset();
			}
			else
			{
				longSilence++;
				if (random.Next(2, 4) < longSilence)
				{
					specialButtons("I'm on the edge", 1);
					setTPText(voc.getVoc("strokingTease"));
					longSilence = 0;
				}
			}
		}
		else if (!holdingBreath && secWindow.getCurrentMediaScreen() != 10)
		{
			longSilence = 0;
			linkStopWatch.Restart();
			pickScript();
		}
	}

	private void Stroke()
	{
		if (!sessionPaused && Interlocked.Exchange(ref strokeTickRunning, 1) == 0)
		{
			Task.Run(delegate
			{
				try
				{
					if (currentState == "anal" || currentState == "cbt" || currentState == "analExtreme" || currentState == "cbtExtreme")
				{
					if (wantsBeatBar)
					{
						hideBeatbar(hide: false);
					}
					muteBeatBar = false;
					stopOna = true;
				}
				else if (isSettingEnabled("wearingChastity"))
				{
					if (wantsBeatBar)
					{
						hideBeatbar();
					}
					muteBeatBar = true;
					currentScript.deleteFlag("ona", temp: true);
				}
				else if (getTFlag("ona"))
				{
					if (wantsBeatBar)
					{
						hideBeatbar();
					}
					muteBeatBar = true;
					stopOna = false;
				}
				if (!stroking)
				{
					sendAllTypes(ona, 0.0);
				}
				if (!isSettingEnabled("wearingChastity") || currentState == "anal" || currentState == "cbt" || currentState == "analExtreme" || currentState == "cbtExtreme")
				{
					if (stroking && !subliminal)
					{
						if (!noBalls)
						{
							long now = Environment.TickCount64;
							if (now - Interlocked.Read(ref lastBallMotionMs) >= 250)
							{
								Interlocked.Exchange(ref lastBallMotionMs, now);
								Task.Run((Action)setBallInMotion);
								ballPitCount++;
							}
						}
						Task.Delay(timeBeforeBeat).ContinueWith(delegate
						{
							beating();
						});
					}
					else if (subliminal && stroking)
					{
						beating();
					}
				}
				int num = (int)(bpm / (float)(10 + imageSpeedAdditive * 7));
				if (num < 1)
				{
					num = 1;
				}
				if (subliminal)
				{
					num = 2;
					if (beatCount % num == 0)
					{
						setNewMedia();
					}
				}
				int num2 = 2000 * int.Parse(getVar("videoMod"));
				if (num2 < -10000)
				{
					num2 = -10000;
				}
				if (secWindow.getCurrentMediaScreen() == 10 && boredByImages.ElapsedMilliseconds > 9000 + random.Next(60000))
				{
					setNewMediaFormat();
				}
				else if (secWindow.mediasCounter >= secWindow.medias.Length && boredByImages.ElapsedMilliseconds > 14000 + random.Next(60000) && secWindow.getCurrentMediaScreen() < 8)
				{
					setNewMediaFormat();
				}
				else if (boredByImages.ElapsedMilliseconds > 25000 + random.Next(80000) + num2 && secWindow.getCurrentMediaScreen() >= 8)
				{
					if (secWindow.getCurrentMediaScreen() <= 9)
					{
						lastMediaWasVideo = true;
						setNewMediaFormat();
					}
					else
					{
						setNewMediaFormat();
					}
				}
				else if (boredByImages.ElapsedMilliseconds > 15000 + random.Next(700000) + num2 && secWindow.getCurrentMediaScreen() >= 8)
				{
					setNewMedia();
					boredByImages.Restart();
				}
				if (secWindow.getCurrentMediaScreen() < 8 && beatCount % num == 0 && !subliminal)
				{
					Task.Delay(timeBeforeBeat).ContinueWith(delegate
					{
						if (secWindow.getCurrentMediaScreen() < 8)
						{
							setNewMedia();
						}
					});
				}
				beatCount++;
				}
				finally
				{
					Interlocked.Exchange(ref strokeTickRunning, 0);
				}
			});
		}
		else
		{
			sendAllTypes(plug, 0.0);
			plug = null;
			sendAllTypes(wand, 0.0);
			wand = null;
			sendAllTypes(ona, 0.0);
			ona = null;
		}
		Task.Delay(TimeSpan.FromSeconds(60f / bpm)).ContinueWith(delegate
		{
			Stroke();
		});
	}

	private void Talking(bool isMain)
	{
		if (performanceLogTimer.Elapsed.TotalSeconds >= 30.0)
		{
			LogSessionPerformanceSnapshot();
			performanceLogTimer.Restart();
		}
		if (sessionLogTimer.Elapsed.TotalMinutes >= 2.0)
		{
			SessionTraceLogger.Memory("session", "periodic state=" + currentState + " script=" + currentScript?.GetType().Name + " elapsed=" + sessionTimer.Elapsed);
			sessionLogTimer.Restart();
		}
		if (!scriptPaused && !currentScript.talkLocked && !sessionPaused)
		{
			sessionTimer.Start();
			switch (currentState)
			{
			case "module":
				setTPText(currentScript.Talk());
				break;
			case "stroke":
				if (!getTFlag("vibe") && !isSettingEnabled("wearingChastity"))
				{
					setVar("state", "0");
				}
				StrokeTaunting();
				break;
			case "edge":
				methodEdgeRelease();
				currentState = "module";
				break;
			case "edgeHold":
				if (firstHoldingEdge)
				{
					if (ona != null && getTFlag("ona"))
					{
						setNewSpeed(60f);
					}
					else if (wand != null && getTFlag("vibe"))
					{
						setNewSpeed(80f);
					}
					else if (plug != null && getTFlag("plug"))
					{
						setNewSpeed(200f);
					}
					setTPText(lr.getVocab("edgeHold"));
					firstHoldingEdge = false;
					watch.Restart();
				}
				else if (watch.ElapsedMilliseconds > secondsOfEdging * 1000 && secondsOfEdging != 0)
				{
					methodEdgeRelease();
					currentState = "module";
					firstHoldingEdge = true;
					secondsOfEdging = 0;
				}
				else if (watch.ElapsedMilliseconds > 8000 + random.Next(12000) && secondsOfEdging == 0)
				{
					methodEdgeRelease();
					currentState = "module";
					firstHoldingEdge = true;
					totalTimeOnEdge += watch.Elapsed.Seconds;
				}
				else if (random.Next(10) > 7)
				{
					setTPText(lr.getVocab("edgeTaunt"));
				}
				break;
			case "breathHold":
				if (random.Next(3, 6) < longSilence)
				{
					longSilence = 0;
					setTPText(voc.getVoc("breathTease"));
				}
				longSilence++;
				break;
			case "cbt":
				setVar("state", "1");
				specialButtons("Please...", 6);
				cbt();
				break;
			case "cbtExtreme":
				setVar("state", "1");
				specialButtons("Please...", 6);
				cbtExtreme();
				break;
			case "anal":
				setVar("state", "2");
				specialButtons("Please...", 6);
				anal();
				break;
			case "vibeNo":
				removeSpecialButtons("Please...", 6);
				removeSpecialButtons("I'm on the edge", 1);
				currentScript = new VibeNo(this, currentScript);
				currentState = "module";
				break;
			case "vibe":
				setVar("state", "3");
				specialButtons("Please...", 6);
				specialButtons("I'm on the edge", 1);
				currentScript = new Vibe(this, currentScript);
				currentState = "module";
				break;
			case "analExtreme":
				specialButtons("Please...", 6);
				setVar("state", "2");
				anal(extreme: true);
				break;
			case "orgasmDecide":
				currentState = "module";
				oD();
				break;
			case "cum":
				currentState = "module";
				break;
			case "ruin":
				stroking = false;
				currentState = "module";
				break;
			case "deny":
				methodDenial();
				currentState = "module";
				break;
			default:
				if (isSettingEnabled("wearingChastity"))
				{
					chastityTaunt();
					break;
				}
				currentState = "stroke";
				watch.Restart();
				linkStopWatch.Restart();
				break;
			}
		}
		else if (scriptPaused)
		{
			sessionTimer.Stop();
		}
		if (isMain)
		{
			Task.Delay(TimeSpan.FromMilliseconds((double)talkingTime * talkSpeed)).ContinueWith(delegate
			{
				Talking(isMain);
			});
		}
	}

	private void LogSessionPerformanceSnapshot()
	{
		try
		{
			DateTime now = DateTime.UtcNow;
			TimeSpan currentCpu;
			long workingSet;
			long privateBytes;
			int threadCount;
			int handleCount;
			using (Process process = Process.GetCurrentProcess())
			{
				currentCpu = process.TotalProcessorTime;
				workingSet = process.WorkingSet64;
				privateBytes = process.PrivateMemorySize64;
				threadCount = process.Threads.Count;
				handleCount = process.HandleCount;
			}
			double wallSeconds = Math.Max(0.001, (now - lastPerformanceSampleUtc).TotalSeconds);
			double cpuPercent = Math.Max(0.0, (currentCpu - lastPerformanceCpuTime).TotalMilliseconds / (wallSeconds * 1000.0 * Math.Max(1, Environment.ProcessorCount)) * 100.0);
			lastPerformanceCpuTime = currentCpu;
			lastPerformanceSampleUtc = now;
			string secondWindowSnapshot = secWindow == null ? "secondWindow=none" : secWindow.GetPerformanceSnapshot();
			SessionTraceLogger.Info("perf", "cpu=" + Math.Round(cpuPercent, 1) + "%"
				+ " threads=" + threadCount
				+ " handles=" + handleCount
				+ " managed=" + FormatBytes(GC.GetTotalMemory(forceFullCollection: false))
				+ " working=" + FormatBytes(workingSet)
				+ " private=" + FormatBytes(privateBytes)
				+ " state=" + currentState
				+ " script=" + (currentScript == null ? "none" : currentScript.GetType().Name)
				+ " bpm=" + Math.Round(bpm, 1)
				+ " stroking=" + stroking
				+ " subliminal=" + subliminal
				+ " beatCount=" + beatCount
				+ " ballPitCount=" + ballPitCount
				+ " media=" + secondWindowSnapshot);
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("perf", "Failed to write performance snapshot", ex);
		}
	}

	private static string FormatBytes(long bytes)
	{
		if (bytes <= 0)
		{
			return "0 MB";
		}
		return Math.Round(bytes / 1024.0 / 1024.0, 1) + " MB";
	}

	public int getFavor()
	{
		base.Dispatcher.Invoke(delegate
		{
			totalCurrency.Text = int.Parse(getVar("favor")).ToString() ?? "";
		});
		return int.Parse(getVar("favor"));
	}

	public void setFavor(int favorVal)
	{
		int oldFavor = int.Parse(getVar("favor"));
		setVar("favor", favorVal.ToString() ?? "");
		p.homeworkScreen.totalCash = favorVal;
		Task.Run(delegate
		{
			base.Dispatcher.Invoke(delegate
			{
				DoubleAnimation animation = new DoubleAnimation(totalCurrency.FontSize, 48.0, TimeSpan.FromMilliseconds(1000.0));
				totalCurrency.BeginAnimation(Page.FontSizeProperty, animation);
				if (favorVal > oldFavor)
				{
					totalCurrency.Foreground = Brushes.Green;
				}
				else if (favorVal < oldFavor)
				{
					totalCurrency.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ad0000");
				}
			});
			float f = oldFavor;
			int num = (int)f;
			while (favorVal - (int)f != 0)
			{
				f = ((float)favorVal + f) / 2f;
				if ((int)f != num)
				{
					num = (int)f;
					base.Dispatcher.Invoke(delegate
					{
						totalCurrency.Text = ((int)f).ToString() ?? "";
						totalCurrency.FontSize = 42.0;
					});
					Thread.Sleep(100);
				}
				Thread.Sleep(50);
			}
			Thread.Sleep(2000);
			base.Dispatcher.Invoke(delegate
			{
				DoubleAnimation animation = new DoubleAnimation(totalCurrency.FontSize, 32.0, TimeSpan.FromMilliseconds(1000.0));
				totalCurrency.BeginAnimation(Page.FontSizeProperty, animation);
				totalCurrency.Foreground = Brushes.White;
				totalCurrency.Text = favorVal.ToString() ?? "";
			});
		});
	}

	private void heartAnimation()
	{
		base.Dispatcher.Invoke(delegate
		{
			new TransformGroup();
			heart.RenderTransform = new ScaleTransform(1.0, 1.0);
			heart.RenderTransformOrigin = new Point(0.5, 0.5);
			Storyboard storyboard = new Storyboard();
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.2, 1.0, new Duration(TimeSpan.FromSeconds(0.8)));
			Storyboard.SetTargetName(doubleAnimation, heart.Name);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(1.2, 1.0, new Duration(TimeSpan.FromSeconds(0.8)));
			Storyboard.SetTargetName(doubleAnimation, heart.Name);
			Storyboard.SetTargetName(doubleAnimation2, heart.Name);
			Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
			Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
			ExponentialEase easingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut
			};
			doubleAnimation.EasingFunction = easingFunction;
			doubleAnimation2.EasingFunction = easingFunction;
			storyboard.Children.Add(doubleAnimation);
			storyboard.Children.Add(doubleAnimation2);
			storyboard.Begin(heart, isControllable: true);
		});
		base.Dispatcher.Invoke(delegate
		{
			new TransformGroup();
			heartWhite.RenderTransform = new ScaleTransform(1.0, 1.0);
			heartWhite.RenderTransformOrigin = new Point(0.5, 0.5);
			Storyboard storyboard = new Storyboard();
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.2, 1.0, new Duration(TimeSpan.FromSeconds(0.8)));
			Storyboard.SetTargetName(doubleAnimation, heartWhite.Name);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(1.2, 1.0, new Duration(TimeSpan.FromSeconds(0.8)));
			Storyboard.SetTargetName(doubleAnimation, heartWhite.Name);
			Storyboard.SetTargetName(doubleAnimation2, heartWhite.Name);
			Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
			Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
			DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.5, 0.0, new Duration(TimeSpan.FromSeconds(0.2)));
			ExponentialEase easingFunction = new ExponentialEase
			{
				EasingMode = EasingMode.EaseOut
			};
			doubleAnimation.EasingFunction = easingFunction;
			doubleAnimation2.EasingFunction = easingFunction;
			doubleAnimation3.EasingFunction = easingFunction;
			Storyboard.SetTargetName(doubleAnimation3, heartWhite.Name);
			Storyboard.SetTargetProperty(doubleAnimation3, new PropertyPath(UIElement.OpacityProperty));
			storyboard.Children.Add(doubleAnimation);
			storyboard.Children.Add(doubleAnimation2);
			storyboard.Children.Add(doubleAnimation3);
			storyboard.Begin(heartWhite, isControllable: true);
		});
	}

	private void beating()
	{
		timeSince = boredByImages.ElapsedMilliseconds;
		if (!muteBeatBar)
		{
			anglePlayer.playSound("tickLong", masterVolumeValue / 2.0 * uiVolumeValue / 2.0);
		}
		if (!subliminal)
		{
			long now = Environment.TickCount64;
			if (now - Interlocked.Read(ref lastHeartAnimationMs) >= 250)
			{
				Interlocked.Exchange(ref lastHeartAnimationMs, now);
				heartAnimation();
			}
		}
		strokeAmount++;
		if (random.Next(0, 150) >= 148 && !subliminal && isSettingEnabled("asmr") && !specialAudioLocked)
		{
			specialAudioLocked = true;
			if (random.Next(2) == 1 && isSettingEnabled("hypno"))
			{
				mantraSpeaker(new HypnosisMantra(this), 10 + random.Next(40));
			}
			else
			{
				anglePlayer.setMoaning(asmrStrings[random.Next(asmrStrings.Length)]);
			}
			specialAudioLocked = false;
		}
	}

	public void strobeCenter(int count = 20)
	{
		if (count > 0)
		{
			if (count % 2 == 0)
			{
				base.Dispatcher.Invoke(delegate
				{
					textCenter.Visibility = Visibility.Hidden;
				}, DispatcherPriority.SystemIdle);
				Thread.Sleep(60);
				strobeCenter(--count);
			}
			else
			{
				base.Dispatcher.Invoke(delegate
				{
					textCenter.Visibility = Visibility.Visible;
				}, DispatcherPriority.SystemIdle);
				Thread.Sleep(30);
				strobeCenter(--count);
			}
		}
		else
		{
			base.Dispatcher.Invoke(delegate
			{
				textCenter.Text = "";
				textCenter.Visibility = Visibility.Visible;
			}, DispatcherPriority.SystemIdle);
		}
	}

	private void spamAudio(string vocString, int timesToRepeatAudio = 2, float volume = 0.05f, int delay = 100)
	{
		try
		{
			SessionTraceLogger.Info("audio", "spamAudio text=" + vocString + " repeat=" + timesToRepeatAudio + " volume=" + volume + " delay=" + delay);
			bool changeEar = true;
			if (random.Next(2) == 0)
			{
				changeEar = false;
			}
			for (int i = 0; i < timesToRepeatAudio; i++)
			{
				Task.Delay(delay).ContinueWith(delegate
				{
					syntheticAudio(vocString, changeEar, volume);
				});
				changeEar = !changeEar;
			}
		}
		catch (Exception ex)
		{
			SessionTraceLogger.Error("audio", "spamAudio failed text=" + vocString, ex);
		}
	}

	private void mantraSpeaker(HypnosisMantra mantra, int duration)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num = 0;
		while (stopwatch.Elapsed.TotalSeconds < (double)duration || num % mantra.allText.Length != 0)
		{
			string mantraString = mantra.Talk();
			num++;
			Task.Run(delegate
			{
				spamAudio(mantraString, 1, 0.05f, 0);
			});
			Thread.Sleep(80 * mantraString.Length + 260);
		}
	}

	private void syntheticAudio(string text, bool leftEar, float volume)
	{
        volume *= (float)(masterVolumeValue / 2.0 * ttsVolumeValue / 2.0);
        tts.Speak(text, volume, leftEar);
    }

	private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
	{
		((WaveOutEvent)sender).Dispose();
	}

	private void strobeText(int even = 0, int generation = 0)
	{
		lock (strobeTextLock)
		{
			if (!strobeTextRunning || generation != strobeTextGeneration)
			{
				SessionTraceLogger.Info("hypnosis-strobe", "exit stale tick=" + even + " generation=" + generation + " current=" + strobeTextGeneration + " running=" + strobeTextRunning);
				return;
			}
		}
		if (even == 0 || even % 100 == 0)
		{
			SessionTraceLogger.Info("hypnosis-strobe", "tick=" + even + " generation=" + generation + " subliminal=" + subliminal + " strobeFlag=" + getFlag("strobe"));
		}
		if (even % 2 == 0)
		{
			if (hypnosisCount > 0)
			{
				hypnosisCount--;
			}
			SolidColorBrush brush = null;
			string text = "";
			if (even % 20 == 0)
			{
				text = lr.getVocab("subliminals");
				if (subliminalAudio)
				{
					Task.Run(delegate
					{
						spamAudio(text);
					});
				}
				brush = new SolidColorBrush(colorCreator());
				brush.Freeze();
				base.Dispatcher.Invoke(delegate
				{
					for (int i = 1; i < textBlocks.Length; i++)
					{
						textBlocks[i].Text = text;
						textBlocks[i].Foreground = brush;
					}
				});
			}
			if (!getFlag("strobe"))
			{
				base.Dispatcher.Invoke(delegate
				{
					for (int i = 1; i < textBlocks.Length; i++)
					{
						textBlocks[i].Visibility = Visibility.Hidden;
					}
				});
			}
			base.Dispatcher.Invoke(delegate
			{
				if (getFlag("strobe"))
				{
					strobeRect.Visibility = Visibility.Collapsed;
				}
				for (int i = 1; i < textBlocks.Length; i++)
				{
					textBlocks[i].Visibility = Visibility.Hidden;
				}
			});
			Thread.Sleep(80);
			Task.Run(delegate
			{
				strobeText(even + 1, generation);
			});
		}
		else
		{
			if (!subliminal)
			{
				lock (strobeTextLock)
				{
					if (generation == strobeTextGeneration)
					{
						strobeTextRunning = false;
					}
				}
				SessionTraceLogger.Info("hypnosis-strobe", "stop tick=" + even + " generation=" + generation + " subliminal=false");
				return;
			}
			if (!getFlag("strobe"))
			{
				base.Dispatcher.Invoke(delegate
				{
					for (int i = 1; i < textBlocks.Length; i++)
					{
						textBlocks[i].Visibility = Visibility.Visible;
					}
				});
			}
			base.Dispatcher.Invoke(delegate
			{
				if (getFlag("strobe"))
				{
					strobeRect.Visibility = Visibility.Visible;
				}
				for (int i = 1; i < textBlocks.Length; i++)
				{
					textBlocks[i].Visibility = Visibility.Visible;
				}
			});
			Thread.Sleep(20);
			Task.Run(delegate
			{
				strobeText(even + 1, generation);
			});
		}
	}

	public void hypnosisAnswer(string text)
	{
		hypnosisCount = 100;
		base.Dispatcher.InvokeAsync(delegate
		{
			if (subliminalAudio)
			{
				Task.Run(delegate
				{
					spamAudio(text, 2, 0.1f);
				});
			}
			else
			{
				voice(text);
				hypnosisCount = 0;
			}
			for (int num = 1; num < textBlocks.Length; num++)
			{
				textBlocks[num].Visibility = Visibility.Hidden;
				textBlocks[num].Text = text;
				textBlocks[num].Foreground = Brushes.White;
			}
		}, DispatcherPriority.SystemIdle);
	}

	public void voice(string text)
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			Task.Run(delegate
			{
				spamAudio(text, 1, 0.2f, 0);
			});
		}, DispatcherPriority.SystemIdle);
	}

	private void createCenterTextAnimations()
	{
		new ExponentialEase().EasingMode = EasingMode.EaseIn;
		int num = 0;
		TextBlock[] array = textBlocks;
		foreach (TextBlock textBlock in array)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.5, 1.2, new Duration(TimeSpan.FromSeconds(0.3)));
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.5, 1.2, new Duration(TimeSpan.FromSeconds(0.3)));
			DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(0.15)));
			doubleAnimation3.AutoReverse = true;
			TransformGroup transformGroup = new TransformGroup();
			transformGroup.Children.Add(new ScaleTransform(1.0, 1.0));
			textBlock.RenderTransform = transformGroup;
			textBlock.RenderTransformOrigin = new Point(0.5, 0.5);
			Storyboard.SetTargetName(doubleAnimation, textBlock.Name);
			Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
			textCenterStoryboards[num].Children.Add(doubleAnimation);
			Storyboard.SetTargetName(doubleAnimation2, textBlock.Name);
			Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
			textCenterStoryboards[num].Children.Add(doubleAnimation2);
			Storyboard.SetTargetName(doubleAnimation3, textBlock.Name);
			Storyboard.SetTargetProperty(doubleAnimation3, new PropertyPath(UIElement.OpacityProperty));
			textCenterStoryboards[num].Children.Add(doubleAnimation3);
			num = 1;
		}
		DoubleAnimation doubleAnimation4 = new DoubleAnimation(1.8, 2.2, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation4.AutoReverse = true;
		doubleAnimation4.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation5 = new DoubleAnimation(1.8, 2.2, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation5.AutoReverse = true;
		doubleAnimation5.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation6 = new DoubleAnimation(0.9, 1.0, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation6.AutoReverse = true;
		doubleAnimation6.RepeatBehavior = RepeatBehavior.Forever;
		TransformGroup transformGroup2 = new TransformGroup();
		transformGroup2.Children.Add(new ScaleTransform(1.0, 1.0));
		hypnoOverlay.RenderTransform = transformGroup2;
		hypnoOverlay.RenderTransformOrigin = new Point(0.5, 0.5);
		Storyboard.SetTargetName(doubleAnimation4, hypnoOverlay.Name);
		Storyboard.SetTargetProperty(doubleAnimation4, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));
		hypnosisOverlayStoryboard.Children.Add(doubleAnimation4);
		Storyboard.SetTargetName(doubleAnimation5, hypnoOverlay.Name);
		Storyboard.SetTargetProperty(doubleAnimation5, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
		hypnosisOverlayStoryboard.Children.Add(doubleAnimation5);
		Storyboard.SetTargetName(doubleAnimation6, hypnoOverlay.Name);
		Storyboard.SetTargetProperty(doubleAnimation6, new PropertyPath(UIElement.OpacityProperty));
		hypnosisOverlayStoryboard.Children.Add(doubleAnimation6);
		hypnosisOverlayStoryboard.SetSpeedRatio(hypnoOverlay, bpm / 60f);
		hypnosisOverlayStoryboard.Begin(hypnoOverlay, isControllable: true);
		TransformGroup transformGroup3 = new TransformGroup();
		transformGroup3.Children.Add(new RotateTransform(0.0));
		transformGroup3.Children.Add(new ScaleTransform(1.0, 1.0));
		spiral.RenderTransform = transformGroup3;
		spiral.RenderTransformOrigin = new Point(0.5, 0.5);
		DoubleAnimation doubleAnimation7 = new DoubleAnimation(0.0, 359.0, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation7.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation8 = new DoubleAnimation(2.0, 2.1, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation8.AutoReverse = true;
		doubleAnimation8.RepeatBehavior = RepeatBehavior.Forever;
		DoubleAnimation doubleAnimation9 = new DoubleAnimation(2.0, 2.1, new Duration(TimeSpan.FromSeconds(3.0)));
		doubleAnimation9.AutoReverse = true;
		doubleAnimation9.RepeatBehavior = RepeatBehavior.Forever;
		Storyboard.SetTargetName(doubleAnimation7, spiral.Name);
		Storyboard.SetTargetProperty(doubleAnimation7, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(RotateTransform.Angle)"));
		Storyboard.SetTargetName(doubleAnimation8, spiral.Name);
		Storyboard.SetTargetProperty(doubleAnimation8, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(ScaleTransform.ScaleY)"));
		Storyboard.SetTargetName(doubleAnimation9, spiral.Name);
		Storyboard.SetTargetProperty(doubleAnimation9, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(ScaleTransform.ScaleX)"));
		spiralStoryBoard.Children.Add(doubleAnimation7);
		spiralStoryBoard.Children.Add(doubleAnimation9);
		spiralStoryBoard.Children.Add(doubleAnimation8);
		spiralStoryBoard.SetSpeedRatio(spiral, bpm / 60f);
		spiralStoryBoard.RepeatBehavior = RepeatBehavior.Forever;
		spiralStoryBoard.Begin(spiral, isControllable: true);
	}

	private Color colorCreator()
	{
		return Color.FromRgb((byte)random.Next(220, 255), (byte)random.Next(20, 120), (byte)random.Next(220, 255));
	}

	private Uri TakeRandomImage()
	{
		if (displayedImagesAmount == imagePaths.Count)
		{
			displayedImagesAmount = 0;
			Array.Fill(displayedImages, -1);
		}
		int num = random.Next(0, imagePaths.Count);
		if (Array.IndexOf(displayedImages, num) == -1)
		{
			displayedImages[displayedImagesAmount] = num;
			displayedImagesAmount++;
			return new Uri(imagePaths[num]);
		}
		return TakeRandomImage();
	}

	private void setTPText(string text)
	{
		if (!(text != ""))
		{
			return;
		}
		if (!subliminal)
		{
			lr.readText(text);
            tts.Speak(text.Replace("\n", ". "), 1f * (float)(masterVolumeValue / 2.0 * ttsVolumeValue / 2.0));
            base.Dispatcher.InvokeAsync(delegate
			{
				TextBlock textBlock = new TextBlock
				{
					Text = text.Trim(),
					FontSize = 48.0,
					Foreground = Brushes.White,
					FontFamily = new FontFamily("Times New Roman"),
					MinWidth = 100.0,
					MaxWidth = 1800.0,
					Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					TextAlignment = TextAlignment.Center
				};
				textBlock.TextWrapping = TextWrapping.Wrap;
				textPanel.Children.Add(textBlock);
				if (textPanel.Children.Count - 2 >= 0)
				{
					setTPOTo0((TextBlock)textPanel.Children[textPanel.Children.Count - 2], 3.0);
					removeinvisChildren();
				}
				if (textPanel.Children.Count - 1 >= 0)
				{
					setTPOTo0((TextBlock)textPanel.Children[textPanel.Children.Count - 1], 10.0);
				}
			}, DispatcherPriority.ApplicationIdle);
		}
		else
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				textCenter.Text = text;
			});
		}
	}

	private void removeinvisChildren()
	{
		if (textPanel.Children[0].Opacity == 0.0)
		{
			textPanel.Children.RemoveAt(0);
			removeinvisChildren();
		}
	}

	public void setTPOTo1Question()
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			if (textPanel.Children.Count > 0)
			{
				DoubleAnimation animation = new DoubleAnimation(textPanel.Children[textPanel.Children.Count - 1].Opacity, 1.0, new Duration(TimeSpan.FromSeconds(1.0)));
				textPanel.Children[textPanel.Children.Count - 1].BeginAnimation(UIElement.OpacityProperty, animation);
			}
		});
	}

	private void setTPOTo0(TextBlock textblock, double speed = 6.0)
	{
		DoubleAnimation animation = new DoubleAnimation(textblock.Opacity, 0.0, new Duration(TimeSpan.FromSeconds(speed)));
		textblock.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	public void createNewButtons(string[] buttonNames, int[] types)
	{
		base.Dispatcher.Invoke(delegate
		{
			if (ShouldShowDevSessionToolsButton() && !buttonNames.Any((string name) => string.Equals(name?.Trim(), "Dev", StringComparison.OrdinalIgnoreCase)))
			{
				buttonNames = buttonNames.Concat(new string[1] { "Dev" }).ToArray();
				types = types.Concat(new int[1] { 7 }).ToArray();
			}
			buttonStackPanel.Children.Clear();
			talkButtons = new Btn[buttonNames.Length];
			for (int i = 0; i < buttonNames.Length; i++)
			{
				Btn btn = new Btn(types[i])
				{
					Content = new TextBlock
					{
						Text = buttonNames[i],
						FontFamily = new FontFamily("Times New Roman"),
						TextDecorations = TextDecorations.Underline,
						Foreground = Brushes.White,
						FontSize = 32.0,
						Margin = new Thickness(18.0, 2.0, 18.0, 2.0)
					}
				};
				talkButtons[i] = btn;
				buttonStackPanel.Children.Add(talkButtons[i]);
				talkButtons[i].Click += talkButtonClick;
			}
		});
	}

	private void talkButtonClick(object sender, RoutedEventArgs e)
	{
		timed++;
		Btn button = (Btn)sender;
		TextBlock textBlock = (TextBlock)button.Content;
		string buttonText = textBlock.Text.Trim();
		if (button.type == 7)
		{
			OpenDevSessionToolsWindow();
			return;
		}
		buttonStackPanel.Visibility = Visibility.Collapsed;
		setTPOTo0((TextBlock)textPanel.Children[textPanel.Children.Count - 1]);
		Task.Run(delegate
		{
			handleButtonPress(buttonText, button.type);
		});
	}

	private bool ShouldShowDevSessionToolsButton()
	{
		return sessionActive && isSettingEnabled("devSessionTools");
	}

	private void OpenDevSessionToolsWindow()
	{
		SessionTraceLogger.Info("dev-tools", "open");
		if (devSessionToolsWindow == null || !devSessionToolsWindow.IsLoaded)
		{
			devSessionToolsWindow = new DevSessionToolsWindow(this);
			devSessionToolsWindow.Closed += delegate
			{
				devSessionToolsWindow = null;
			};
			devSessionToolsWindow.Show();
		}
		else
		{
			devSessionToolsWindow.Activate();
		}
	}

	private void handleButtonPress(string btnString, int type)
	{
		base.Dispatcher.Invoke(delegate
		{
			p.playClickSound();
			secWindow.hideCaptionImage();
			removeSpecialButtons("Please...", 6);
			currentScript.buttonClicked(btnString);
			removeTalkButtons(btnString);
			watch.Restart();
			switch (type)
			{
			case 1:
				if (promissedEdges > 0)
				{
					promissedEdges--;
					scriptPaused = true;
					stroking = false;
					setTPText(lr.getVocab("edgeStop"));
					Task.Delay(random.Next(4000, 14000)).ContinueWith(delegate
					{
						if (random.Next(10) > 7)
						{
							methodEdgeHold();
						}
						else
						{
							methodEdge();
						}
					});
					edgesDone++;
				}
				else if (!edgeAllowed)
				{
					linkStopWatch.Restart();
					edgesDone++;
					currentScript.repeating = false;
					currentScript = new IllegalEdge(this, currentScript);
					scriptPaused = false;
					currentState = "module";
				}
				else
				{
					scriptPaused = false;
					if (currentState == "breathHold" && getTFlag("ona"))
					{
						setNewSpeed(100f);
					}
				}
				edgesDone++;
				specialButtons("I came", 3);
				break;
			case 2:
				currentScript = new IllegalBreath(this, currentScript);
				setTPText(currentScript.Talk());
				break;
			case 3:
				currentScript = new IllegalCum(this, currentScript);
				scriptPaused = false;
				currentState = "module";
				removeSpecialButtons("I'm on the edge", 1);
				stroking = false;
				break;
			case 4:
				currentScript = new Disobedience(this);
				scriptPaused = false;
				stroking = false;
				break;
			case 5:
				scriptPaused = false;
				removeSpecialButtons("I'm on the edge", 1);
				break;
			case 6:
				scriptPaused = false;
				removeSpecialButtons("I'm on the edge", 1);
				removeSpecialButtons("I came", 3);
				currentScript = new Request(this, currentScript, currentState);
				currentState = "module";
				linked = false;
				break;
			default:
				scriptPaused = false;
				break;
			}
			hasEdged++;
			Task.Delay(400).ContinueWith(delegate
			{
				base.Dispatcher.Invoke(delegate
				{
					Talking(isMain: false);
					buttonStackPanel.Visibility = Visibility.Visible;
				});
			});
		});
	}

	public void removeTalkButtons(string clickedButton)
	{
		base.Dispatcher.Invoke(delegate
		{
			int count = buttonStackPanel.Children.Count;
			int num = 0;
			for (int i = 0; i < count; i++)
			{
				Btn btn = (Btn)buttonStackPanel.Children[num];
				string text = ((TextBlock)btn.Content).Text.Trim();
				if (btn.type != 0 && btn.type != 4 && text != clickedButton)
				{
					num++;
				}
				else
				{
					buttonStackPanel.Children.RemoveAt(num);
				}
			}
		});
	}

	private void specialButtons(string text, int type)
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			bool flag = true;
			_ = type;
			_ = 6;
			List<string> list = new List<string>();
			List<int> list2 = new List<int>();
			foreach (Btn child in buttonStackPanel.Children)
			{
				if (child.type == type)
				{
					flag = false;
					break;
				}
				TextBlock textBlock = (TextBlock)child.Content;
				list.Add(textBlock.Text);
				list2.Add(child.type);
			}
			if (flag)
			{
				createNewButtons(new string[1] { text }.Concat(list).ToArray(), new int[1] { type }.Concat(list2).ToArray());
			}
		});
	}

	public void setTalkSpeed(double textSpeed)
	{
		if (textSpeed != 1.0)
		{
			if (textSpeed != 2.0)
			{
				if (textSpeed != 3.0)
				{
					if (textSpeed != 4.0)
					{
						if (textSpeed == 5.0)
						{
							talkSpeed = 0.7;
						}
					}
					else
					{
						talkSpeed = 0.8;
					}
				}
				else
				{
					talkSpeed = 1.0;
				}
			}
			else
			{
				talkSpeed = 1.3;
			}
		}
		else
		{
			talkSpeed = 1.6;
		}
	}

	private void removeSpecialButtons(string text, int type)
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			foreach (Btn child in buttonStackPanel.Children)
			{
				if (child.type == type)
				{
					buttonStackPanel.Children.Remove(child);
					break;
				}
			}
		});
	}

	private void cumButtons()
	{
		string[] buttonNames = new string[1] { "cumming" };
		int[] types = new int[1] { 5 };
		createNewButtons(buttonNames, types);
	}

	public void endSession()
	{
		base.Dispatcher.InvokeAsync(delegate
		{
			Application.Current.Shutdown();
		});
	}

	private void Button_MouseEnter(object sender, MouseEventArgs e)
	{
	}

	public void createTextField(string textName, string correctAnswer = "")
	{
		acceptAnswer = correctAnswer;
		base.Dispatcher.Invoke(delegate
		{
			userText.IsEnabled = true;
			userText.Visibility = Visibility.Visible;
		});
		scriptPaused = true;
		askedForUserInput = textName;
	}

	private void completeUserText()
	{
		setVar(askedForUserInput, userText.Text);
		scriptPaused = false;
		userText.IsEnabled = false;
		userText.Visibility = Visibility.Collapsed;
		userText.Text = "";
		Keyboard.Focus(Window.GetWindow(this));
	}

	private void userText_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key.ToString() == "Return" && userText.Text.Length > 2)
		{
			completeUserText();
		}
	}
}
