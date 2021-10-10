using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.Animations;

using AvatarDescriptor = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using ExpressionsMenu = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu;
using ExpressionsControl = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using ExpressionParameters = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;
using ExpressionParameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using RuntimeAnimatorController = UnityEngine.RuntimeAnimatorController;

public class FlyAssistGUI : EditorWindow
{
  // 設定用情報
  private AvatarDescriptor target_avatar;
  private GameObject cube;
  // 調整値の種類
  private static readonly string[] HandTypes = { "Left", "Right" };
  private static readonly string[] GestureTypes = { "0. Neutral", "1. Fist", "2. Open", "3. Point", "4. Victory", "5. Rock", "6. Gun", "7. ThumbsUp" };

  // デフォルト値
  private const string param_name = "FlyCollider";
  private const string root = "Assets/coke12103/FlyAssist";

  private const string default_fx_path = "Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3HandsLayer.controller";
  private const string default_ex_menu_path = "Assets/VRCSDK/Examples3/Expressions Menu/DefaultExpressionsMenu.asset";
  private const string default_ex_param_path = "Assets/VRCSDK/Examples3/Expressions Menu/DefaultExpressionParameters.asset";

  private const int fx_index = 4;

  // 調整値
  private int hand_num;
  private int gesture_num;

  // その他
  private string user_asset_path;

  // その他
  private string message;

  [MenuItem("FlyAssist/Editor")]
  private static void Create(){
    FlyAssistGUI win = GetWindow<FlyAssistGUI>("FlyAssist");
  }

  private void OnGUI(){
    EditorGUILayout.LabelField("FlyAssist");

    target_avatar = EditorGUILayout.ObjectField("Avatar", target_avatar, typeof(AvatarDescriptor), true) as AvatarDescriptor;

    // そもそもAvatarがなければ無効化する
    EditorGUI.BeginDisabledGroup(target_avatar == null);
      hand_num = EditorGUILayout.Popup("Hand", hand_num, HandTypes);
      gesture_num = EditorGUILayout.Popup("Gesture", gesture_num, GestureTypes);

      EditorGUILayout.HelpBox(message, MessageType.Info);

      EditorGUI.BeginDisabledGroup(!CheckCondition());
        if(GUILayout.Button("Install")){
          try{
            CheckInstallCondition();
          }catch(System.Exception e){
            Debug.Log(e.ToString());
            message = "Error: " + e.Message;
            return;
          }
          Install();
        }
      EditorGUI.EndDisabledGroup();

      if(GUILayout.Button("Uninstall")){
        Uninstall();
      }
    EditorGUI.EndDisabledGroup();
  }

  bool CheckCondition(){
    if(target_avatar == null) return false;

    return true;
  }

  void Install(){
    SetupDirs();
    SetupDescriptor();
    // ここから削除処理
    Uninstall();
    // ここまで削除処理
    // ここから追加処理
    CreateAnimatorParamAndLayer();
    CreateCollider();
    CreateAndBuildAnimation();
    CreateExParam();
    CreateExMenu();
    // ここまで追加処理
    SaveAssets();

    message = "Install done!\n生成されたCubeの位置やサイズを調整してMeshレンダラーを削除してください！";
  }

  void Uninstall(){
    RemoveOldParams();
    RemoveOldLeyers();
    RemoveOldExParam();
    RemoveOldExMenu();
    SaveAssets();

    message = "Uninstall done!\nCubeは自分で消してください！";
  }

  void CheckInstallCondition(){
    string result = "";

    if(!AssetDatabase.IsValidFolder("Assets/VRCSDK")) result = "VRCSDKのフォルダがない";

    if(result != "") throw new System.Exception(result);
  }

  void SetupDirs(){
    string invalid_regex_text = "[" + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]";
    Regex invalid_regex = new Regex(invalid_regex_text);

    string valid_name = invalid_regex.Replace(target_avatar.name, "_");
    user_asset_path = root + "/User/" + valid_name;

    if(!AssetDatabase.IsValidFolder(root + "/User")) AssetDatabase.CreateFolder(root, "User");
    if(!AssetDatabase.IsValidFolder(user_asset_path)) AssetDatabase.CreateFolder(root + "/User", valid_name);
  }

  bool IsFxExist(){
    AvatarDescriptor.CustomAnimLayer layer = target_avatar.baseAnimationLayers[fx_index];
    return (layer.isDefault || layer.animatorController == null);
  }

  // FX/EX Menu/EX Paramがないアバターに対応する。
  void SetupDescriptor(){
    // baseAnimationLayers
    // base, additive, gesture, action, fx
    if(IsFxExist()){
      // FXない処理
      target_avatar.customizeAnimationLayers = true;

      string fx_path = user_asset_path + "/FXLayer.controller";

      AssetDatabase.CopyAsset(default_fx_path, fx_path);
      target_avatar.baseAnimationLayers[fx_index].isDefault = false;
      target_avatar.baseAnimationLayers[fx_index].isEnabled = true;
      target_avatar.baseAnimationLayers[fx_index].animatorController = AssetDatabase.LoadAssetAtPath(fx_path, typeof(RuntimeAnimatorController)) as RuntimeAnimatorController;

      Debug.Log("FX作った");
    }

    if(!target_avatar.customExpressions) target_avatar.customExpressions = true;

    if(target_avatar.expressionsMenu == null){
      // ExMenuない処理
      string ex_menu_path = user_asset_path + "/ExMenu.asset";

      AssetDatabase.CopyAsset(default_ex_menu_path, ex_menu_path);
      target_avatar.expressionsMenu = AssetDatabase.LoadAssetAtPath(ex_menu_path, typeof(ExpressionsMenu)) as ExpressionsMenu;

      Debug.Log("ExMenu作った");
    }

    if(target_avatar.expressionParameters == null){
      // ExParamない処理
      string ex_param_path = user_asset_path + "/ExParam.asset";

      AssetDatabase.CopyAsset(default_ex_param_path, ex_param_path);
      target_avatar.expressionParameters = AssetDatabase.LoadAssetAtPath(ex_param_path, typeof(ExpressionParameters)) as ExpressionParameters;

      Debug.Log("ExParam作った");
    }
  }

  void RemoveOldParams(){
    // 新規作成時には絶対にある、削除時にはない場合がある。
    if(IsFxExist()){
      Debug.Log("レイヤーがないのでスキップ");
      return;
    }

    AnimatorController fx_layer = target_avatar.baseAnimationLayers[fx_index].animatorController as AnimatorController;

    AnimatorControllerParameter[] orig_params = fx_layer.parameters;
    AnimatorControllerParameter[] removed_params = new AnimatorControllerParameter[orig_params.Length];

    int count = 0;

    for(int i = 0; i < orig_params.Length; i++){
      AnimatorControllerParameter param = orig_params[i];

      if(!(param.name == param_name)){
        removed_params[count] = param;
        count++;
      }else{
        Debug.Log("Removed: " + param.name);
      }
    }

    System.Array.Resize(ref removed_params, count);

    fx_layer.parameters = removed_params;
  }

  void RemoveOldLeyers(){
    // 新規作成時には絶対にある、削除時にはない場合がある。
    if(IsFxExist()){
      Debug.Log("レイヤーがないのでスキップ");
      return;
    }

    AnimatorController fx_layer = target_avatar.baseAnimationLayers[fx_index].animatorController as AnimatorController;

    AnimatorControllerLayer[] orig_layers = fx_layer.layers;
    AnimatorControllerLayer[] removed_layers = new AnimatorControllerLayer[orig_layers.Length];

    int count = 0;

    for(int i = 0; i < orig_layers.Length; i++){
      AnimatorControllerLayer layer = orig_layers[i];

      if(!(layer.name == param_name)){
        removed_layers[count] = layer;
        count++;
      }else{
        Debug.Log("Removed: " + layer.name);
      }
    }

    System.Array.Resize(ref removed_layers, count);

    fx_layer.layers = removed_layers;
  }

  void RemoveOldExParam(){
    // 新規作成時には絶対にある、削除時にはない場合がある。
    if(target_avatar.expressionParameters == null){
      Debug.Log("EX Paramがないのでスキップ");
      return;
    }

    ExpressionParameters ex_param = target_avatar.expressionParameters;

    ExpressionParameter[] orig_ex_params = ex_param.parameters;
    ExpressionParameter[] removed_ex_params = new ExpressionParameter[orig_ex_params.Length];

    int count = 0;

    for(int i = 0; i < orig_ex_params.Length; i++){
      ExpressionParameter param = orig_ex_params[i];

      if(!(param.name == param_name) && !(param.name == "")){
        removed_ex_params[count] = param;
        count++;
      }else{
        Debug.Log("Removed: " + param.name);
      }
    }

    System.Array.Resize(ref removed_ex_params, count);

    ex_param.parameters = removed_ex_params;
  }

  void RemoveOldExMenu(){
    // 新規作成時には絶対にある、削除時にはない場合がある。
    if(target_avatar.expressionsMenu == null){
      Debug.Log("EX Menuがないのでスキップ");
      return;
    }

    ExpressionsMenu ex_menu = target_avatar.expressionsMenu;

    int i = 0;
    while(i < ex_menu.controls.Count){
      if(ex_menu.controls[i].name == param_name){
        Debug.Log("Removed: " + ex_menu.controls[i].name);
        ex_menu.controls.RemoveAt(i);
        continue;
      }else{
        i++;
      }
    }
  }

  bool IsParamExist(string name){
    AnimatorController fx_layer = target_avatar.baseAnimationLayers[fx_index].animatorController as AnimatorController;

    AnimatorControllerParameter[] _params = fx_layer.parameters;

    for(int i = 0; i < _params.Length; i++){
      AnimatorControllerParameter param = _params[i];

      if(param.name == name) return true;
    }

    return false;
  }

  void CreateAnimatorParamAndLayer(){
    AnimatorController fx_layer = target_avatar.baseAnimationLayers[fx_index].animatorController as AnimatorController;

    if(!IsParamExist("GestureLeft")) fx_layer.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);
    if(!IsParamExist("GestureRight")) fx_layer.AddParameter("GestureRight", AnimatorControllerParameterType.Int);
    if(!IsParamExist("VRMode")) fx_layer.AddParameter("VRMode", AnimatorControllerParameterType.Int);
    fx_layer.AddParameter(param_name, AnimatorControllerParameterType.Bool);
    fx_layer.AddLayer(param_name);

    FixLayerWeight(fx_layer);
  }

  void FixLayerWeight(AnimatorController anim_con){
    AnimatorControllerLayer[] orig_layers = anim_con.layers;
    AnimatorControllerLayer[] fixed_layers = new AnimatorControllerLayer[orig_layers.Length];

    for(int i = 0; i < orig_layers.Length; i++){
      AnimatorControllerLayer layer = orig_layers[i];

      if(layer.name == param_name){
        layer.defaultWeight = 1.0f;
        Debug.Log("Weight fix: " + layer.name);
      }

      fixed_layers[i] = layer;
    }

    anim_con.layers = fixed_layers;
  }

  void CreateCollider(){
    cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

    cube.transform.position = target_avatar.gameObject.transform.position;
    cube.transform.parent = target_avatar.gameObject.transform;
  }

  void CreateAndBuildAnimation(){
    AnimatorController fx_layer = target_avatar.baseAnimationLayers[fx_index].animatorController as AnimatorController;
    Transform cube_trans = cube.transform;

    AnimationClip off_anim = new AnimationClip();
    AddCurve(off_anim, cube_trans, typeof(GameObject), "m_IsActive", 0);
    AssetDatabase.CreateAsset(off_anim, user_asset_path + "/off.anim");

    AnimatorState off_state = CreateState(fx_layer, param_name, off_anim);

    AnimatorStateTransition off_desktop_transition = CreateAnyStateTransition(fx_layer, param_name, off_state);
    AnimatorStateTransition off_vr_transition = CreateAnyStateTransition(fx_layer, param_name, off_state);

    off_desktop_transition.AddCondition(AnimatorConditionMode.IfNot, 0, param_name);
    off_vr_transition.AddCondition(AnimatorConditionMode.If, 0, param_name);
    off_vr_transition.AddCondition(AnimatorConditionMode.Equals, 1, "VRMode");
    off_vr_transition.AddCondition(AnimatorConditionMode.NotEqual, gesture_num, hand_num == 0 ? "GestureLeft" : "GestureRight");

    AnimationClip on_anim = new AnimationClip();
    AddCurve(on_anim, cube_trans, typeof(GameObject), "m_IsActive", 1);
    AssetDatabase.CreateAsset(on_anim, user_asset_path + "/on.anim");

    AnimatorState on_state = CreateState(fx_layer, param_name, on_anim);

    AnimatorStateTransition on_desktop_transition = CreateAnyStateTransition(fx_layer, param_name, on_state);
    AnimatorStateTransition on_vr_transition = CreateAnyStateTransition(fx_layer, param_name, on_state);

    on_desktop_transition.AddCondition(AnimatorConditionMode.If, 0, param_name);
    on_desktop_transition.AddCondition(AnimatorConditionMode.Equals, 0, "VRMode");
    on_vr_transition.AddCondition(AnimatorConditionMode.If, 0, param_name);
    on_vr_transition.AddCondition(AnimatorConditionMode.Equals, 1, "VRMode");
    on_vr_transition.AddCondition(AnimatorConditionMode.Equals, gesture_num, hand_num == 0 ? "GestureLeft" : "GestureRight");

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
  }

  void CreateExParam(){
    ExpressionParameters ex_param = target_avatar.expressionParameters;

    ExpressionParameter[] ex_params = ex_param.parameters;

    AddExParam(ref ex_params, param_name, ExpressionParameters.ValueType.Bool);

    ex_param.parameters = ex_params;
  }

  void CreateExMenu(){
    ExpressionsMenu ex_menu = target_avatar.expressionsMenu;

    ExpressionsControl toggle_control = CreateExControl("Fly", ExpressionsControl.ControlType.Toggle, param_name, 1);
    ex_menu.controls.Add(toggle_control);
  }

  void SaveAssets(){
    if(target_avatar.baseAnimationLayers[fx_index].animatorController != null) EditorUtility.SetDirty(target_avatar.baseAnimationLayers[fx_index].animatorController);
    if(target_avatar.expressionsMenu != null) EditorUtility.SetDirty(target_avatar.expressionsMenu);
    if(target_avatar.expressionParameters != null) EditorUtility.SetDirty(target_avatar.expressionParameters);

    EditorUtility.SetDirty(target_avatar);

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
  }

  void AddCurve(AnimationClip clip, Transform target, System.Type target_type, string key, float value){
    AnimationCurve curve = new AnimationCurve();

    // 親はavatarで固定
    string path = AnimationUtility.CalculateTransformPath(target, target_avatar.gameObject.transform);

    curve.AddKey(0, value);

    clip.SetCurve(path, target_type, key, curve);
  }

  AnimatorState CreateState(AnimatorController anim, string layer_name, Motion motion){
    AnimatorControllerLayer[] layers = anim.layers;

    AnimatorStateMachine state_machine = layers[GetLayerIndex(anim, layer_name)].stateMachine;

    AnimatorState state = state_machine.AddState(motion.name);

    state.motion = motion;

    EditorUtility.SetDirty(state_machine);

    // NOTE: UnityのドキュメントにはLayersはコピーだから変更したら戻せよって書いてあるんだけど何故か上書きしなくても反映されてしかもディスクへの書き出しまでされる。怖いから一応やる。
    anim.layers = layers;

    return state;
  }

  AnimatorStateTransition CreateAnyStateTransition(AnimatorController anim, string layer_name, AnimatorState state){
    AnimatorControllerLayer[] layers = anim.layers;

    AnimatorStateMachine machine = layers[GetLayerIndex(anim, layer_name)].stateMachine;

    AnimatorStateTransition transition = machine.AddAnyStateTransition(state);

    transition.duration = 0;
    transition.hasExitTime = false;

    EditorUtility.SetDirty(machine);

    anim.layers = layers;

    return transition;
  }

  // ない場合は想定しない(実装的にない場合は例外なので)
  int GetLayerIndex(AnimatorController anim, string name){
    AnimatorControllerLayer[] layers = anim.layers;

    for(int i = 0; i < layers.Length; i++){
      AnimatorControllerLayer layer = layers[i];

      if(layer.name == name){
        return i;
      }
    }

    throw new System.Exception("そんなやつない");
  }

  void AddExParam(ref ExpressionParameter[] ex_params, string name, ExpressionParameters.ValueType type){
    System.Array.Resize(ref ex_params, ex_params.Length + 1);

    ex_params[ex_params.Length -1] = new ExpressionParameter();
    ex_params[ex_params.Length -1].name = name;
    ex_params[ex_params.Length -1].valueType = type;
  }

  ExpressionsControl CreateExControl(string name, ExpressionsControl.ControlType type, string param_name, int value){
    ExpressionsControl ex_control = new ExpressionsControl();

    ex_control.name = name;
    ex_control.type = type;

    if(type != ExpressionsControl.ControlType.SubMenu && type != ExpressionsControl.ControlType.RadialPuppet){
      ex_control.parameter = new ExpressionsControl.Parameter();
      ex_control.parameter.name = param_name;
    }else if(type == ExpressionsControl.ControlType.RadialPuppet){
      ex_control.subParameters = new ExpressionsControl.Parameter[1];
      ex_control.subParameters[0] = new ExpressionsControl.Parameter();
      ex_control.subParameters[0].name = param_name;
    }

    if(type == ExpressionsControl.ControlType.Toggle){
      ex_control.value = value;
    }

    return ex_control;
  }
}
