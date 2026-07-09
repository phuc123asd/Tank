import sys, re

def fix_prefab(file_path, remove_network_object=False):
    with open(file_path, 'r') as f:
        content = f.read()

    # Fix NetworkTransform
    nt_pattern = re.compile(r'--- !u!114 &(-?\d+)\nMonoBehaviour:.*?m_EditorClassIdentifier: Unity\.Netcode\.Runtime::Unity\.Netcode\.Components\.NetworkTransform.*?(?=\n---|$)', re.DOTALL)
    nt_match = nt_pattern.search(content)
    if nt_match:
        nt_id = nt_match.group(1)
        print(f"Found NetworkTransform with ID {nt_id} in {file_path}")
        ref_pattern = re.compile(r'    - targetCorrespondingSourceObject: [^\n]+\n      insertIndex: -?[0-9]+\n      addedObject: \{fileID: ' + nt_id + r'\}\n')
        content = ref_pattern.sub('', content)
        content = content.replace(nt_match.group(0) + '\n', '')
    
    if remove_network_object:
        no_pattern = re.compile(r'--- !u!114 &(-?\d+)\nMonoBehaviour:.*?m_EditorClassIdentifier: Unity\.Netcode\.Runtime::Unity\.Netcode\.NetworkObject.*?(?=\n---|$)', re.DOTALL)
        no_match = no_pattern.search(content)
        if no_match:
            no_id = no_match.group(1)
            print(f"Found NetworkObject with ID {no_id} in {file_path}")
            ref_pattern = re.compile(r'    - targetCorrespondingSourceObject: [^\n]+\n      insertIndex: -?[0-9]+\n      addedObject: \{fileID: ' + no_id + r'\}\n')
            content = ref_pattern.sub('', content)
            content = content.replace(no_match.group(0) + '\n', '')

    # Fix empty m_AddedComponents to []
    content = re.sub(r'm_AddedComponents:\n(?=  m_SourcePrefab:)', 'm_AddedComponents: []\n', content)

    with open(file_path, 'w') as f:
        f.write(content)

base_dir = "Assets/_Tanks/Game/Prefabs/Tanks/"
fix_prefab(base_dir + "Tank - Medium Variant.prefab")
fix_prefab(base_dir + "Tank - Shark Variant.prefab")
fix_prefab(base_dir + "Tank - Heavy Variant.prefab")
fix_prefab(base_dir + "Tank - ATV Variant.prefab", remove_network_object=True)
