import os
import re

# 작업 대상 디렉토리 (sxtg2-mod)
BASE_DIR = r"h:\source\repos\sxtg2\sxtg2-mod"

merge_groups = [
    {
        "name": "SceneDetector",
        "target": "Features/SceneDetector/SceneDetector.cs",
        "sources": [
            "Features/SceneDetector/SceneDetector.cs",
            "Features/SceneDetector/SceneDetector.Events.cs",
            "Features/SceneDetector/SceneDetector.Lifecycle.cs",
            "Features/SceneDetector/SceneDetector.Update.cs"
        ],
        "delete_sources": [
            "Features/SceneDetector/SceneDetector.Events.cs",
            "Features/SceneDetector/SceneDetector.Lifecycle.cs",
            "Features/SceneDetector/SceneDetector.Update.cs"
        ],
        "class_name": "SceneDetector",
        "namespace": "sxtg2.Features"
    },
    {
        "name": "TrackDataAnalyzer",
        "target": "Features/TrackDataAnalyzer.cs",
        "sources": [
            "Features/TrackDataAnalyzer.cs",
            "Features/TrackDataAnalyzer.AlbumFolders.cs",
            "Features/TrackDataAnalyzer.Difficulty.cs",
            "Features/TrackDataAnalyzer.Inject.cs",
            "Features/TrackDataAnalyzer.TrackInfo.cs",
            "Features/TrackDataAnalyzer.TrackList.cs"
        ],
        "delete_sources": [
            "Features/TrackDataAnalyzer.AlbumFolders.cs",
            "Features/TrackDataAnalyzer.Difficulty.cs",
            "Features/TrackDataAnalyzer.Inject.cs",
            "Features/TrackDataAnalyzer.TrackInfo.cs",
            "Features/TrackDataAnalyzer.TrackList.cs"
        ],
        "class_name": "TrackDataAnalyzer",
        "namespace": "sxtg2.Features"
    },
    {
        "name": "PauseMethodHelper",
        "target": "Helpers/Pause/PauseMethodHelper.cs",
        "sources": [
            "Helpers/Pause/PauseMethodHelper.cs",
            "Helpers/Pause/PauseMethodHelper.UI.cs"
        ],
        "delete_sources": [
            "Helpers/Pause/PauseMethodHelper.UI.cs"
        ],
        "class_name": "PauseMethodHelper",
        "namespace": "sxtg2.Helpers"
    },
    {
        "name": "BGMPlayerHook",
        "target": "Hooks/Audio/BGMPlayerHook.cs",
        "sources": [
            "Hooks/Audio/BGMPlayerHook.cs",
            "Hooks/Audio/BGMPlayerHook.Playback.cs"
        ],
        "delete_sources": [
            "Hooks/Audio/BGMPlayerHook.Playback.cs"
        ],
        "class_name": "BGMPlayerHook",
        "namespace": "sxtg2.Hooks.Audio"
    },
    {
        "name": "ManagerMusicSelectHook",
        "target": "Hooks/Manager/ManagerMusicSelectHook.cs",
        "sources": [
            "Hooks/Manager/ManagerMusicSelectHook.cs",
            "Hooks/Manager/ManagerMusicSelectHook.AlbumFinder.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.AudioSource.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.FileResolver.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.Audio.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.AudioSources.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.FileResolver.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Thumbnail.cs"
        ],
        "delete_sources": [
            "Hooks/Manager/ManagerMusicSelectHook.AlbumFinder.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.AudioSource.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Demo.FileResolver.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.Audio.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.AudioSources.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Preview.FileResolver.cs",
            "Hooks/Manager/ManagerMusicSelectHook.Thumbnail.cs"
        ],
        "class_name": "ManagerMusicSelectHook",
        "namespace": "sxtg2.Hooks.Manager"
    },
    {
        "name": "ManagerPlayHook",
        "target": "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.cs",
        "sources": [
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.cs",
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.Initialize.cs",
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.Pause.cs",
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.PlayStart.cs"
        ],
        "delete_sources": [
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.Initialize.cs",
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.Pause.cs",
            "Hooks/Manager/ManagerPlayHook/ManagerPlayHook.PlayStart.cs"
        ],
        "class_name": "ManagerPlayHook",
        "namespace": "sxtg2.Hooks.Manager"
    },
    {
        "name": "SXGTDataHook",
        "target": "Hooks/SXGT/SXGTDataHook.cs",
        "sources": [
            "Hooks/SXGT/SXGTDataHook.cs",
            "Hooks/SXGT/SXGTDataHook.NoteOps.Constructors.cs",
            "Hooks/SXGT/SXGTDataHook.NoteOps.cs",
            "Hooks/SXGT/SXGTDataHook.NoteTypeExtraction.cs",
            "Hooks/SXGT/SXGTDataHook.Pending.cs"
        ],
        "delete_sources": [
            "Hooks/SXGT/SXGTDataHook.NoteOps.Constructors.cs",
            "Hooks/SXGT/SXGTDataHook.NoteOps.cs",
            "Hooks/SXGT/SXGTDataHook.NoteTypeExtraction.cs",
            "Hooks/SXGT/SXGTDataHook.Pending.cs"
        ],
        "class_name": "SXGTDataHook",
        "namespace": "sxtg2.Hooks.SXGT"
    },
    {
        "name": "TextHook",
        "target": "Hooks/Text/TextHook.cs",
        "sources": [
            "Hooks/Text/TextHook.cs",
            "Hooks/Text/TextHook.BmsLoader.cs",
            "Hooks/Text/TextHook.BmsLoader.Search.cs",
            "Hooks/Text/TextHook.Initialization.cs",
            "Hooks/Text/TextHook.TrackFinder.cs"
        ],
        "delete_sources": [
            "Hooks/Text/TextHook.BmsLoader.cs",
            "Hooks/Text/TextHook.BmsLoader.Search.cs",
            "Hooks/Text/TextHook.Initialization.cs",
            "Hooks/Text/TextHook.TrackFinder.cs"
        ],
        "class_name": "TextHook",
        "namespace": "sxtg2.Hooks.Text"
    },
    {
        "name": "BmsParser",
        "target": "Loaders/BmsParser.cs",
        "sources": [
            "Loaders/BmsParser.cs",
            "Loaders/BmsParser.Processing.cs"
        ],
        "delete_sources": [
            "Loaders/BmsParser.Processing.cs"
        ],
        "class_name": "BmsParser",
        "namespace": "sxtg2.Loaders"
    },
    {
        "name": "Main",
        "target": "Main/Main.cs",
        "sources": [
            "Main/Main.cs",
            "Main/Main.BmsBootstrap.cs",
            "Main/Main.Initialization.cs",
            "Main/Main.UpdateLoop.cs"
        ],
        "delete_sources": [
            "Main/Main.BmsBootstrap.cs",
            "Main/Main.Initialization.cs",
            "Main/Main.UpdateLoop.cs"
        ],
        "class_name": "Main",
        "namespace": "sxtg2"
    },
    {
        "name": "CustomChartInjector",
        "target": "Processors/CustomChartInjector.cs",
        "sources": [
            "Processors/CustomChartInjector.cs",
            "Processors/CustomChartInjector.FieldSetter.cs",
            "Processors/CustomChartInjector.NoteConstructors.cs",
            "Processors/CustomChartInjector.NoteEnums.cs",
            "Processors/CustomChartInjector.NoteFactory.cs"
        ],
        "delete_sources": [
            "Processors/CustomChartInjector.FieldSetter.cs",
            "Processors/CustomChartInjector.NoteConstructors.cs",
            "Processors/CustomChartInjector.NoteEnums.cs",
            "Processors/CustomChartInjector.NoteFactory.cs"
        ],
        "class_name": "CustomChartInjector",
        "namespace": "sxtg2.Processors"
    }
]

def extract_class_declaration(content, class_name):
    pattern = r'\b(public|internal|private|protected|static|partial|\s)*(class|struct|interface)\s+' + re.escape(class_name) + r'\b[^{]*'
    match = re.search(pattern, content)
    if match:
        decl = match.group(0).strip()
        decl = re.sub(r'\s+', ' ', decl)
        return decl
    return f"public partial class {class_name}"

def extract_class_body(content, class_name):
    match = re.search(r'\b(class|struct|interface)\s+' + re.escape(class_name) + r'\b', content)
    if not match:
        return ""
    pos = match.start()
    
    brace_start = content.find('{', pos)
    if brace_start == -1:
        return ""
    
    has_namespace = "namespace " in content
    
    r_content = content[::-1]
    first_brace_rev = r_content.find('}')
    if first_brace_rev == -1:
        return ""
        
    if has_namespace:
        second_brace_rev = r_content.find('}', first_brace_rev + 1)
        if second_brace_rev == -1:
            return ""
        class_close_idx = len(content) - 1 - second_brace_rev
    else:
        class_close_idx = len(content) - 1 - first_brace_rev
        
    return content[brace_start + 1 : class_close_idx]

def merge_group(group):
    name = group["name"]
    target_rel = group["target"]
    sources_rel = group["sources"]
    class_name = group["class_name"]
    ns = group["namespace"]
    
    target_path = os.path.join(BASE_DIR, target_rel.replace("/", "\\"))
    print(f"Merging group '{name}' -> target: {target_path}")
    
    all_usings = set()
    all_assembly_attrs = set()
    all_bodies = []
    
    for src_rel in sources_rel:
        src_path = os.path.join(BASE_DIR, src_rel.replace("/", "\\"))
        if not os.path.exists(src_path):
            print(f"  [Warning] File not found: {src_path}")
            continue
            
        with open(src_path, "r", encoding="utf-8") as f:
            content = f.read()
            
        # using 문 추출 (C# using block 'using ( ... )' 은 매칭에서 제외)
        usings = re.findall(r'^\s*using\s+[^;(\n]+;', content, re.MULTILINE)
        for u in usings:
            all_usings.add(u.strip())
            
        # assembly 레벨 속성 추출 ([assembly: ...])
        assembly_attrs = re.findall(r'^\[assembly:\s+[^\]]+\]', content, re.MULTILINE)
        for attr in assembly_attrs:
            all_assembly_attrs.add(attr.strip())
            
        # class body 추출
        body = extract_class_body(content, class_name)
        if body:
            all_bodies.append(body.strip("\r\n"))
        else:
            print(f"  [Warning] Failed to extract body from {src_path}")
            
    sorted_usings = sorted(list(all_usings))
    using_block = "\n".join(sorted_usings)
    
    sorted_assembly_attrs = sorted(list(all_assembly_attrs))
    assembly_block = "\n".join(sorted_assembly_attrs)
    if assembly_block:
        assembly_block = "\n\n" + assembly_block
        
    merged_body = "\n\n        // ==========================================\n        // Merged from separate partial files\n        // ==========================================\n\n".join(all_bodies)
    
    # 첫 번째 소스 파일의 내용에서 정확한 클래스 선언(상속 및 지시어 포함)을 추출합니다.
    first_src_path = os.path.join(BASE_DIR, sources_rel[0].replace("/", "\\"))
    with open(first_src_path, "r", encoding="utf-8") as f:
        first_content = f.read()
        
    class_decl = extract_class_declaration(first_content, class_name)
    
    final_content = f"""{using_block}{assembly_block}

namespace {ns}
{{
    {class_decl}
    {{
{merged_body}
    }}
}}
"""
    with open(target_path, "w", encoding="utf-8") as f:
        f.write(final_content)
        
    print(f"  Successfully wrote merged content to {target_path}")

def clean_and_update():
    deleted_files = []
    for group in merge_groups:
        for del_rel in group["delete_sources"]:
            del_path = os.path.join(BASE_DIR, del_rel.replace("/", "\\"))
            if os.path.exists(del_path):
                os.remove(del_path)
                deleted_files.append(del_rel.replace("/", "\\"))
                print(f"Deleted file: {del_path}")
                
    csproj_path = os.path.join(BASE_DIR, "sxtg2.csproj")
    if os.path.exists(csproj_path):
        with open(csproj_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
            
        new_lines = []
        skip_count = 0
        for line in lines:
            should_skip = False
            for del_file in deleted_files:
                if f'Include="{del_file}"' in line or f'Include="{del_file.replace("\\\\", "\\")}"' in line:
                    should_skip = True
                    break
            if should_skip:
                skip_count += 1
                print(f"Removing reference from csproj: {line.strip()}")
            else:
                new_lines.append(line)
                
        with open(csproj_path, "w", encoding="utf-8") as f:
            f.writelines(new_lines)
            
        print(f"Updated csproj: removed {skip_count} references.")

if __name__ == "__main__":
    print("=== Start Merging partial classes ===")
    for group in merge_groups:
        merge_group(group)
    print("=== Start Cleaning deleted source files & updating csproj ===")
    clean_and_update()
    print("=== Merge process finished ===")
