# 🔤 타입 시스템

**런타임 타입 추출 및 리플렉션 활용**

---

## 게임 타입 구조

```csharp
// 노트 타입 계층
RhythmGame.Note (base)
├── RhythmGame.ShortNote
└── RhythmGame.HoldNote

// Enum 타입
RhythmGame.NoteType
├── SHORT
├── HOLD
├── OPEN
└── ...

RhythmGame.NoteColor
├── BLUE
├── RED
└── OPEN
```

---

## 타입 추출 방법

### 1. laneData에서 추출

```csharp
private static Type _shortNoteType;
private static Type _holdNoteType;

public static void ExtractNoteDataTypeFromLaneData(object sxgtData)
{
    var laneDataField = sxgtData.GetType().GetField("laneData");
    var laneData = laneDataField.GetValue(sxgtData);

    for (int lane = 0; lane <= 9; lane++)
    {
        var noteList = laneData[lane];
        
        if (noteList.Count > 0)
        {
            var firstNote = noteList[0];
            var noteType = firstNote.GetType();
            
            if (noteType.Name.Contains("ShortNote"))
            {
                _shortNoteType = noteType;
                MelonLogger.Msg($"ShortNote 타입 발견: {noteType.FullName}");
            }
            else if (noteType.Name.Contains("HoldNote"))
            {
                _holdNoteType = noteType;
                MelonLogger.Msg($"HoldNote 타입 발견: {noteType.FullName}");
            }
            
            if (_shortNoteType != null && _holdNoteType != null)
                break;
        }
    }
}
```

### 2. Assembly 검색

```csharp
public static Type FindTypeByName(string typeName)
{
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
    
    foreach (var assembly in assemblies)
    {
        try
        {
            var types = assembly.GetTypes();
            
            foreach (var type in types)
            {
                if (type.Name == typeName || type.FullName == typeName)
                {
                    return type;
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // 일부 어셈블리는 로드 실패 가능
            continue;
        }
    }
    
    return null;
}
```

### 3. 제네릭 타입 추출

```csharp
public static Type ExtractGenericType(object list)
{
    var listType = list.GetType();
    
    if (listType.IsGenericType)
    {
        var genericArgs = listType.GetGenericArguments();
        if (genericArgs.Length > 0)
        {
            return genericArgs[0];
        }
    }
    
    return null;
}

// 사용 예시
var laneData = sxgtData.laneData;
var lane0 = laneData[0]; // List<Note>
var noteType = ExtractGenericType(lane0); // Note 타입
```

---

## 생성자 찾기

```csharp
public static ConstructorInfo FindConstructor(Type type, Type[] paramTypes)
{
    var constructors = type.GetConstructors(
        BindingFlags.Public | 
        BindingFlags.NonPublic | 
        BindingFlags.Instance
    );
    
    foreach (var ctor in constructors)
    {
        var parameters = ctor.GetParameters();
        
        if (parameters.Length == paramTypes.Length)
        {
            bool match = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != paramTypes[i])
                {
                    match = false;
                    break;
                }
            }
            
            if (match)
                return ctor;
        }
    }
    
    return null;
}

// 사용 예시
var shortNoteConstructor = FindConstructor(
    _shortNoteType,
    new Type[] { typeof(float), typeof(int) }
);

var holdNoteConstructor = FindConstructor(
    _holdNoteType,
    new Type[] { typeof(float), typeof(int), typeof(float) }
);
```

---

## 필드 접근

```csharp
public static FieldInfo FindFieldCaseInsensitive(Type type, string fieldName)
{
    var fields = type.GetFields(
        BindingFlags.Public | 
        BindingFlags.NonPublic | 
        BindingFlags.Instance
    );
    
    foreach (var field in fields)
    {
        if (field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
        {
            return field;
        }
    }
    
    return null;
}

// 필드 설정
public static void SetNoteField(object note, string fieldName, object value)
{
    var field = FindFieldCaseInsensitive(note.GetType(), fieldName);
    
    if (field != null)
    {
        try
        {
            field.SetValue(note, value);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"필드 설정 실패: {fieldName} = {value}");
            MelonLogger.Error(ex.Message);
        }
    }
}
```

---

## Enum 처리

```csharp
public static object GetEnumValue(string enumTypeName, string valueName)
{
    // 1. Enum 타입 찾기
    var enumType = FindTypeByName(enumTypeName);
    if (enumType == null || !enumType.IsEnum)
    {
        MelonLogger.Error($"Enum 타입을 찾을 수 없음: {enumTypeName}");
        return null;
    }
    
    // 2. Enum 값 가져오기
    try
    {
        return Enum.Parse(enumType, valueName);
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"Enum 값 파싱 실패: {enumTypeName}.{valueName}");
        return null;
    }
}

// 사용 예시
var noteType = GetEnumValue("NoteType", "SHORT");
var noteColor = GetEnumValue("NoteColor", "BLUE");

SetNoteField(note, "nType", noteType);
SetNoteField(note, "nColor", noteColor);
```

---

## 타입 캐싱

```csharp
public class TypeCache
{
    // 노트 타입
    private static Type _shortNoteType;
    private static Type _holdNoteType;
    
    // Enum 타입
    private static Type _noteTypeEnum;
    private static Type _noteColorEnum;
    
    // 생성자
    private static ConstructorInfo _shortNoteConstructor;
    private static ConstructorInfo _holdNoteConstructor;
    
    public static Type ShortNoteType
    {
        get
        {
            if (_shortNoteType == null)
                ExtractTypes();
            return _shortNoteType;
        }
    }
    
    public static Type HoldNoteType
    {
        get
        {
            if (_holdNoteType == null)
                ExtractTypes();
            return _holdNoteType;
        }
    }
    
    private static void ExtractTypes()
    {
        // laneData에서 추출
        // ...
    }
    
    public static void Clear()
    {
        _shortNoteType = null;
        _holdNoteType = null;
        _noteTypeEnum = null;
        _noteColorEnum = null;
        _shortNoteConstructor = null;
        _holdNoteConstructor = null;
    }
}
```

---

## 동적 객체 생성

```csharp
public static object CreateShortNote(float timing, int lane)
{
    var type = TypeCache.ShortNoteType;
    var constructor = type.GetConstructor(new[] { typeof(float), typeof(int) });
    
    var note = constructor.Invoke(new object[] { timing, lane });
    
    // 필드 설정
    SetNoteField(note, "nType", GetEnumValue("NoteType", "SHORT"));
    SetNoteField(note, "nColor", GetNoteColor(lane));
    SetNoteField(note, "targetLane", lane);
    
    return note;
}

public static object CreateHoldNote(float timing, int lane, float duration)
{
    var type = TypeCache.HoldNoteType;
    var constructor = type.GetConstructor(new[] { 
        typeof(float), typeof(int), typeof(float) 
    });
    
    var note = constructor.Invoke(new object[] { timing, lane, duration });
    
    // 필드 설정
    SetNoteField(note, "nType", GetEnumValue("NoteType", "HOLD"));
    SetNoteField(note, "nColor", GetNoteColor(lane));
    SetNoteField(note, "targetLane", lane);
    SetNoteField(note, "duration", duration);
    
    // tickTime 생성
    var tickTime = GenerateTickTimeArray(timing, duration);
    SetNoteField(note, "tickTime", tickTime);
    SetNoteField(note, "tickLength", tickTime.Length);
    
    return note;
}
```

---

## 타입 호환성 체크

```csharp
public static bool IsCompatibleType(Type sourceType, Type targetType)
{
    // 1. 같은 타입
    if (sourceType == targetType)
        return true;
    
    // 2. 상속 관계
    if (targetType.IsAssignableFrom(sourceType))
        return true;
    
    // 3. 인터페이스 구현
    if (targetType.IsInterface && sourceType.GetInterfaces().Contains(targetType))
        return true;
    
    return false;
}

// 사용 예시
var noteType = note.GetType();
var listType = noteList.GetType().GetGenericArguments()[0];

if (IsCompatibleType(noteType, listType))
{
    noteList.Add(note); // 안전
}
```

---

## 리플렉션 최적화

```csharp
// 델리게이트 캐싱
private static Func<object, object> _getTimingDelegate;

public static float GetNoteTiming(object note)
{
    if (_getTimingDelegate == null)
    {
        var field = note.GetType().GetField("timing");
        _getTimingDelegate = (obj) => field.GetValue(obj);
    }
    
    return (float)_getTimingDelegate(note);
}

// Expression Tree 사용
public static Func<object, T> CreateGetter<T>(Type type, string fieldName)
{
    var field = type.GetField(fieldName);
    var param = Expression.Parameter(typeof(object), "obj");
    var cast = Expression.Convert(param, type);
    var access = Expression.Field(cast, field);
    var convert = Expression.Convert(access, typeof(T));
    
    return Expression.Lambda<Func<object, T>>(convert, param).Compile();
}
```

---

## 타입 정보 로깅

```csharp
public static void LogTypeInfo(Type type)
{
    MelonLogger.Msg($"=== Type: {type.FullName} ===");
    
    // 필드
    MelonLogger.Msg("Fields:");
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        MelonLogger.Msg($"  {field.FieldType.Name} {field.Name}");
    }
    
    // 프로퍼티
    MelonLogger.Msg("Properties:");
    foreach (var prop in type.GetProperties())
    {
        MelonLogger.Msg($"  {prop.PropertyType.Name} {prop.Name}");
    }
    
    // 메서드
    MelonLogger.Msg("Methods:");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        MelonLogger.Msg($"  {method.ReturnType.Name} {method.Name}({parameters})");
    }
}
