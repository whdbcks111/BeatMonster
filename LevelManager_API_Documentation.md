# LevelManager API Documentation

## Overview
**Namespace:** `_02.Scripts.Manager`
**Inheritance:** `MonoBehaviour`
**Requirements:** `AudioSource` component

LevelManager는 리듬 게임의 핵심 게임플레이 로직을 관리하는 싱글톤 매니저 클래스입니다. 레벨 로드, 재생 제어, 노트 관리, 판정 시스템, 체크포인트 등의 기능을 제공합니다.

---

## Table of Contents
- [Properties](#properties)
  - [Static Properties](#static-properties)
  - [Public Properties](#public-properties)
  - [Serialized Fields](#serialized-fields)
- [Methods](#methods)
  - [Level Management](#level-management)
  - [Playback Control](#playback-control)
  - [Note Management](#note-management)
  - [Event Management](#event-management)
  - [Checkpoint System](#checkpoint-system)
  - [Time Conversion](#time-conversion)
- [Nested Types](#nested-types)
  - [Level](#level)
  - [Note](#note)
  - [LevelEvent](#levelevent)
  - [LevelPlayerData](#levelplayerdata)
  - [JudgementTimeSettings](#judgementtimesettings)
  - [JudgementType](#judgementtype)

---

## Properties

### Static Properties

#### `instance`
```csharp
public static LevelManager instance { get; private set; }
```
**Description:** 싱글톤 인스턴스
**Access:** Public getter, Private setter

---

### Public Properties

#### `dspTime`
```csharp
public double dspTime { get; }
```
**Description:** 보간된 DSP 시간 (오디오 동기화용)
**Returns:** 현재 보간된 DSP 타임스탬프

#### `isPlaying`
```csharp
public bool isPlaying { get; private set; }
```
**Description:** 레벨이 현재 재생 중인지 여부
**Returns:** `true` if playing, `false` otherwise

#### `currentPlayTime`
```csharp
public float currentPlayTime { get; }
```
**Description:** 현재 재생 시간 (초 단위)
**Returns:** 누적된 재생 시간 (일시정지 고려)

#### `currentBeat`
```csharp
public float currentBeat { get; }
```
**Description:** 현재 비트 위치
**Returns:** BPM 기반 현재 비트 값, 레벨이 없으면 -1

#### `isLoaded`
```csharp
public bool isLoaded { get; set; }
```
**Description:** 레벨 로드 완료 여부
**Returns:** `true` if level is loaded

---

### Serialized Fields

#### Gameplay Settings
- **`offset`** (float): 글로벌 오디오 오프셋 (기본값: 0.24초)
- **`player`** (Player): 플레이어 객체 참조
- **`currentLevelPlayerData`** (LevelPlayerData): 현재 플레이 데이터
- **`currentLevel`** (Level): 현재 로드된 레벨
- **`currentBoss`** (Boss): 현재 보스 객체
- **`noteObjects`** (List\<NoteObject\>): 생성된 노트 객체 리스트
- **`judgementTimeSettings`** (JudgementTimeSettings): 판정 타이밍 설정
- **`playAtLoaded`** (bool): 로드 완료 시 자동 재생 여부

#### Camera Settings
- **`playingViewportStart`** (Vector2): 플레이 영역 시작 뷰포트 좌표
- **`playingViewportEnd`** (Vector2): 플레이 영역 끝 뷰포트 좌표

#### Prefabs
- **`attackPoint`** (GameObject): 공격 노트 히트 포인트 프리팹
- **`defendPoint`** (GameObject): 방어 노트 히트 포인트 프리팹

#### Event Callbacks
- **`onLoaded`** (Action): 레벨 로드 완료 시 호출
- **`onAddJudgement`** (Action\<JudgementType\>): 판정 추가 시 호출

---

## Methods

### Level Management

#### `LoadLevel(string path)`
```csharp
public async UniTask LoadLevel(string path)
```
**Description:** 경로로부터 레벨 데이터를 로드합니다.
**Parameters:**
- `path` (string): 레벨 파일 경로. `$`로 시작하면 Resources 폴더에서 로드
  - 예: `"$Test"` → Resources/Test.json
  - 예: `"C:/Levels/myLevel.json"` → 절대 경로

**Returns:** `UniTask` - 비동기 작업
**Throws:**
- `Exception` - 보스 로드 실패 시
- `Exception` - 음악 파일 로드 실패 시

**Example:**
```csharp
await LevelManager.instance.LoadLevel("$Test");
```

---

#### `LoadLevel(Level level)`
```csharp
public async UniTask LoadLevel(Level level)
```
**Description:** Level 객체로부터 레벨을 로드합니다.
**Parameters:**
- `level` (Level): 로드할 레벨 데이터

**Returns:** `UniTask` - 비동기 작업

---

#### `SaveLevel(string path)`
```csharp
public async UniTask SaveLevel(string path)
```
**Description:** 현재 레벨을 파일로 저장합니다.
**Parameters:**
- `path` (string): 저장 경로. `$`로 시작하면 Resources 폴더에 저장

**Returns:** `UniTask` - 비동기 작업

**Example:**
```csharp
await LevelManager.instance.SaveLevel("$MyLevel");
```

---

### Playback Control

#### `Play()`
```csharp
public void Play()
```
**Description:** 레벨 재생을 시작하거나 재개합니다.
**Note:** 이미 재생 중이면 무시됩니다.

---

#### `Pause()`
```csharp
public void Pause()
```
**Description:** 레벨 재생을 일시정지합니다.
**Side Effects:**
- 모든 SFX 정지
- DSP 시간 누적

---

#### `Stop()`
```csharp
public void Stop()
```
**Description:** 재생을 정지하고 레벨을 초기화합니다.
**Side Effects:** `InitGame()` 호출

---

#### `Seek(double time, float prepareTime = 0f)`
```csharp
public void Seek(double time, float prepareTime = 0f)
```
**Description:** 특정 시간으로 이동합니다.
**Parameters:**
- `time` (double): 목표 시간 (초)
- `prepareTime` (float): 준비 시간 (기본값: 0초)

**Side Effects:**
- 노트 상태 업데이트 (히트 여부)
- 이벤트 재실행/취소
- 보스 HP 재계산
- 스킬볼 리셋

**Example:**
```csharp
// 10초 위치로 이동하고 2초 준비 시간
LevelManager.instance.Seek(10f, 2f);
```

---

### Note Management

#### `AddPattern(Note newNote)`
```csharp
public NoteObject AddPattern(Note newNote)
```
**Description:** 새로운 노트를 패턴에 추가합니다.
**Parameters:**
- `newNote` (Note): 추가할 노트 데이터

**Returns:** `NoteObject` - 생성된 노트 객체

**Example:**
```csharp
var note = new Note
{
    appearBeat = 16f,
    noteType = "Attack"
};
var noteObj = LevelManager.instance.AddPattern(note);
```

---

#### `GetNextNote()`
```csharp
public NoteObject GetNextNote()
```
**Description:** 다음으로 쳐야 할 노트를 반환합니다.
**Returns:** `NoteObject` - 다음 노트, 없으면 `null`
**Logic:**
- 미스 판정 범위 이내의 미히트 노트 중
- 가장 빠른 appearBeat를 가진 노트 반환

---

### Event Management

#### `AddEvent(LevelEvent levelEvent)`
```csharp
public LevelEvent AddEvent(LevelEvent levelEvent)
```
**Description:** 레벨 이벤트를 추가합니다.
**Parameters:**
- `levelEvent` (LevelEvent): 추가할 이벤트

**Returns:** `LevelEvent` - 추가된 이벤트

---

#### `PerformEvent(LevelEvent e)`
```csharp
public void PerformEvent(LevelEvent e)
```
**Description:** 레벨 이벤트를 실행합니다.
**Parameters:**
- `e` (LevelEvent): 실행할 이벤트

**Behavior:**
- 체크포인트 이벤트인 경우 체크포인트 설정
- `isPerformed` 플래그 설정

---

### Checkpoint System

#### `SetCheckpoint(float time)`
```csharp
public void SetCheckpoint(float time)
```
**Description:** 체크포인트를 설정합니다.
**Parameters:**
- `time` (float): 체크포인트 시간 (초)

**Side Effects:** 체크포인트 VFX 트리거

---

#### `GotoCheckpoint()`
```csharp
public void GotoCheckpoint()
```
**Description:** 마지막 체크포인트로 이동합니다.
**Behavior:**
- 체크포인트가 없으면 0초로 이동
- 4비트 준비 시간 제공

---

### Time Conversion

#### `BeatToPlayTime(float beat)`
```csharp
public float BeatToPlayTime(float beat)
```
**Description:** 비트를 재생 시간으로 변환합니다.
**Parameters:**
- `beat` (float): 비트 값

**Returns:** `float` - 재생 시간 (초)
**Formula:** `beat * 60 / BPM`

**Example:**
```csharp
float time = LevelManager.instance.BeatToPlayTime(16f);
// BPM 120일 때: 16 * 60 / 120 = 8초
```

---

#### `PlayTimeToBeat(float time)`
```csharp
public float PlayTimeToBeat(float time)
```
**Description:** 재생 시간을 비트로 변환합니다.
**Parameters:**
- `time` (float): 재생 시간 (초)

**Returns:** `float` - 비트 값
**Formula:** `time * BPM / 60`

---

## Nested Types

### Level
```csharp
[Serializable]
public class Level
```

**Description:** 레벨 데이터 구조체

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `loadedPath` | string | 로드된 경로 (non-serialized) |
| `startOffset` | float | 음악 시작 오프셋 (초) |
| `beatsPerMeasure` | int | 한 마디당 비트 수 |
| `defaultBpm` | int | 기본 BPM |
| `baseScrollSpeed` | float | 기본 스크롤 속도 |
| `bossId` | string | 보스 Addressable ID |
| `musicPath` | string | 음악 파일 경로 |
| `levelName` | string | 레벨 이름 |
| `musicName` | string | 음악 이름 |
| `authorName` | string | 제작자 이름 |
| `backgroundId` | string | 배경 ID |
| `groundId` | string | 그라운드 ID |
| `pattern` | List\<Note\> | 노트 패턴 리스트 |
| `events` | List\<LevelEvent\> | 레벨 이벤트 리스트 |

---

### Note
```csharp
[Serializable]
public class Note
```

**Description:** 노트 데이터 구조체

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `appearBeat` | float | 노트가 나타나는 비트 |
| `noteType` | string | 노트 타입 ID |

#### Methods
- **`Clone()`**: 노트 복사본 생성

---

### LevelEvent
```csharp
[Serializable]
public class LevelEvent
```

**Description:** 레벨 이벤트 구조체 (체크포인트 등)

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `appearBeat` | float | 이벤트 발생 비트 |
| `isCheckpoint` | bool | 체크포인트 여부 |
| `isPerformed` | bool | 실행 여부 (non-serialized) |
| `preventActions` | List\<Action\> | 취소 시 실행할 액션 리스트 |

#### Methods
- **`Clone()`**: 이벤트 복사본 생성
- **`Prevent()`**: 이벤트 취소 및 preventActions 실행

---

### LevelPlayerData
```csharp
[Serializable]
public class LevelPlayerData
```

**Description:** 플레이어 판정 데이터

#### Properties
| Property | Type | Description |
|----------|------|-------------|
| `respawnCount` | int | 리스폰 횟수 |
| `perfectCount` | int | Perfect 판정 횟수 |
| `goodCount` | int | Good 판정 횟수 |
| `lateCount` | int | Late 판정 횟수 |
| `earlyCount` | int | Early 판정 횟수 |
| `missCount` | int | Miss 판정 횟수 |

#### Methods

##### `GetAccuracy()`
```csharp
public float GetAccuracy()
```
**Description:** 정확도를 계산합니다.
**Returns:** 0.0 ~ 1.0 사이의 정확도 값
**Formula:**
```
score = perfect + good * 0.8 + (late + early) * 0.5
maxScore = perfect + good + late + early + miss + respawn * 2
accuracy = score / maxScore
```

---

##### `AddJudgement(float inputTime, float originTime, JudgementTimeSettings settings)`
```csharp
public JudgementType AddJudgement(float inputTime, float originTime, JudgementTimeSettings judgementTimeSettings)
```
**Description:** 판정을 추가하고 카운트를 업데이트합니다.
**Parameters:**
- `inputTime` (float): 입력 시간
- `originTime` (float): 원래 노트 시간
- `judgementTimeSettings` (JudgementTimeSettings): 판정 설정

**Returns:** `JudgementType` - 판정 결과

**Side Effects:**
- 해당 판정 카운트 증가
- `LevelManager.instance.onAddJudgement` 이벤트 호출

**Judgement Logic:**
```
offset = |inputTime - originTime|

if offset <= perfect → Perfect
else if offset <= good → Good
else if offset <= bad:
    if inputTime < originTime → Early
    else → Late
else → Miss
```

---

### JudgementTimeSettings
```csharp
[Serializable]
public struct JudgementTimeSettings
```

**Description:** 판정 타이밍 설정

#### Fields
| Field | Type | Description |
|-------|------|-------------|
| `miss` | float | Miss 판정 임계값 (초) |
| `perfect` | float | Perfect 판정 임계값 (초) |
| `good` | float | Good 판정 임계값 (초) |
| `bad` | float | Bad 판정 임계값 (초) |

---

### JudgementType
```csharp
public enum JudgementType
```

**Description:** 판정 타입 열거형

#### Values
- `Miss` - 빗나감
- `Perfect` - 완벽
- `Good` - 좋음
- `Late` - 늦음
- `Early` - 빠름

---

## Usage Examples

### Basic Level Loading and Playback
```csharp
// 레벨 로드
await LevelManager.instance.LoadLevel("$Test");

// 재생
LevelManager.instance.Play();

// 일시정지
LevelManager.instance.Pause();

// 다시 재생
LevelManager.instance.Play();
```

### Level Editor: Adding Notes
```csharp
// 새 노트 추가
var note = new Note
{
    appearBeat = 32f,
    noteType = "Attack"
};
LevelManager.instance.AddPattern(note);

// 레벨 저장
await LevelManager.instance.SaveLevel("$MyCustomLevel");
```

### Checkpoint System
```csharp
// 체크포인트 설정 (16비트 위치)
var checkpointTime = LevelManager.instance.BeatToPlayTime(16f);
LevelManager.instance.SetCheckpoint(checkpointTime);

// 체크포인트로 복귀
LevelManager.instance.GotoCheckpoint();
```

### Time Conversion
```csharp
// 비트 → 시간
float time = LevelManager.instance.BeatToPlayTime(64f);

// 시간 → 비트
float beat = LevelManager.instance.PlayTimeToBeat(12.5f);
```

### Event Handling
```csharp
// 로드 완료 이벤트 구독
LevelManager.instance.onLoaded += () =>
{
    Debug.Log("Level loaded!");
};

// 판정 이벤트 구독
LevelManager.instance.onAddJudgement += (judgement) =>
{
    Debug.Log($"Judgement: {judgement}");
};
```

---

## Internal Implementation Notes

### DSP Time Interpolation
- `InterpolateDspTime()`: AudioSettings.dspTime이 업데이트되지 않을 때 Time.unscaledDeltaTime으로 보간
- 더 부드러운 오디오 동기화 제공

### Seek Implementation
- 노트 상태 재계산: `wasHit` 플래그 업데이트
- 이벤트 재실행/취소: appearBeat 기준 정렬 후 처리
- 오디오 스케줄링: `PlayScheduled()` 또는 즉시 재생

### Checkpoint VFX
- 로그 스케일 반지름 증가 애니메이션
- 셰이더 프로퍼티: `_Radius`, `_Center`

---

## Dependencies

### Required Components
- `AudioSource`: 음악 재생용
- `Camera.main`: 뷰포트 계산용

### External Dependencies
- **UniTask**: 비동기 작업
- **Addressables**: 보스/음악 리소스 로드
- **UnityWebRequest**: 로컬 오디오 파일 로드

### Related Classes
- `Player`: 플레이어 제어
- `Boss`: 보스 객체
- `NoteObject`: 노트 게임 오브젝트
- `SoundManager`: 사운드 이펙트 관리

---

## File Location
`Assets/02.Scripts/Manager/LevelManager.cs`

---

**Generated:** 2025-12-18
**Version:** 1.0
**Unity Version:** 2022.3+
