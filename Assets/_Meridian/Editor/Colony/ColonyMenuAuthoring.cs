using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
namespace Meridian.Editor
{
    public static class ColonyMenuAuthoring
    {
        [MenuItem("Meridian/Colony/Wire Complete Main Menu")]
        public static void Upgrade()
        {
            const string path="Assets/_Meridian/Prefabs/UI/MainMenu.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller=root.GetComponent<MainMenuPresentation>();var first=root.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text=="NEW COLONY");
                string[] names={"NEW COLONY","CONTINUE","LOAD COLONY","SETTINGS","QUIT"};var actions=new UnityEngine.Events.UnityAction[]{controller.OpenPlanetSelection,controller.ContinueColony,controller.LoadColony,controller.OpenSettings,controller.QuitGame};var buttons=new Button[5];int firstIndex=first.transform.GetSiblingIndex();
                for(int i=0;i<names.Length;i++)
                {
                    string label=names[i];var button=root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.GetComponentInChildren<TMP_Text>().text==label);
                    if(!button){var copy=Object.Instantiate(first.gameObject,first.transform.parent);button=copy.GetComponent<Button>();copy.name=label;}
                    button.name=label;button.transform.SetSiblingIndex(firstIndex+i);var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(92,-(290+77*i));var text=button.GetComponentInChildren<TMP_Text>();text.text=label;button.interactable=true;button.GetComponent<MenuButtonVisual>().Configure(text,button.GetComponentInChildren<CanvasGroup>());
                    while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);UnityEventTools.AddPersistentListener(button.onClick,actions[i]);buttons[i]=button;
                }
                for(int i=0;i<5;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[(i+4)%5],selectOnDown=buttons[(i+1)%5]};
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("Meridian main menu: New Colony, Continue, Load, Settings and Quit wired.");
        }
    }
}
