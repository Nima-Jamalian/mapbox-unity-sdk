﻿namespace Mapbox.Editor
{
	using System.Collections.Generic;
	using System.Linq;
	using UnityEngine;
	using UnityEditor;
	using Mapbox.Unity.Map;
	using UnityEditor.IMGUI.Controls;
	using Mapbox.Unity.MeshGeneration.Modifiers;
	using Mapbox.VectorTile.ExtensionMethods;
	using Mapbox.Unity.MeshGeneration.Filters;
	using Mapbox.Platform.TilesetTileJSON;
	using System;

	public class FeaturesSubLayerPropertiesDrawer
	{
		static float _lineHeight = EditorGUIUtility.singleLineHeight;
		GUIContent[] _sourceTypeContent;
		bool _isGUIContentSet = false;
		bool _isInitialized = false;
		private TileJsonData tileJSONData;
		private static TileJSONResponse tileJSONResponse;
		static TileJsonData tileJsonData = new TileJsonData();
		int _layerIndex = 0;
		GUIContent[] _layerTypeContent;
		bool showModeling = false;
		bool showTexturing = false;
		/// <summary>
		/// Gets or sets the layerID
		/// </summary>
		/// <value><c>true</c> then show general section; otherwise hide, <c>false</c>.</value>

		string objectId = "";
		private string TilesetId
		{
			get
			{
				return EditorPrefs.GetString(objectId + "VectorSubLayerProperties_tilesetId");
			}
			set
			{
				EditorPrefs.SetString(objectId + "VectorSubLayerProperties_tilesetId", value);
			}
		}

		bool ShowPosition
		{
			get
			{
				return EditorPrefs.GetBool(objectId + "VectorSubLayerProperties_showPosition");
			}
			set
			{
				EditorPrefs.SetBool(objectId + "VectorSubLayerProperties_showPosition", value);
			}
		}

		int SelectionIndex
		{
			get
			{
				return EditorPrefs.GetInt(objectId + "VectorSubLayerProperties_selectionIndex");
			}
			set
			{
				EditorPrefs.SetInt(objectId + "VectorSubLayerProperties_selectionIndex", value);
			}
		}

		private GUIStyle visualizerNameAndType = new GUIStyle();
		ModelingSectionDrawer _modelingSectionDrawer = new ModelingSectionDrawer();
		GameplaySectionDrawer _gameplaySectionDrawer = new GameplaySectionDrawer();

		FeatureSubLayerTreeView layerTreeView = new FeatureSubLayerTreeView(new TreeViewState());
		IList<int> selectedLayers = new List<int>();
		public void DrawUI(SerializedProperty property)
		{
			objectId = property.serializedObject.targetObject.GetInstanceID().ToString();
			var serializedMapObject = property.serializedObject;
			AbstractMap mapObject = (AbstractMap)serializedMapObject.targetObject;
			tileJSONData = mapObject.VectorData.LayerProperty.tileJsonData;

			var sourceTypeProperty = property.FindPropertyRelative("_sourceType");
			var sourceTypeValue = (VectorSourceType)sourceTypeProperty.enumValueIndex;

			var displayNames = sourceTypeProperty.enumDisplayNames;
			int count = sourceTypeProperty.enumDisplayNames.Length;
			if (!_isGUIContentSet)
			{
				_sourceTypeContent = new GUIContent[count];
				for (int extIdx = 0; extIdx < count; extIdx++)
				{
					_sourceTypeContent[extIdx] = new GUIContent
					{
						text = displayNames[extIdx],
						tooltip = ((VectorSourceType)extIdx).Description(),
					};
				}
				_isGUIContentSet = true;
			}

			sourceTypeValue = (VectorSourceType)sourceTypeProperty.enumValueIndex;

			var sourceOptionsProperty = property.FindPropertyRelative("sourceOptions");
			var layerSourceProperty = sourceOptionsProperty.FindPropertyRelative("layerSource");
			var layerSourceId = layerSourceProperty.FindPropertyRelative("Id");
			var isActiveProperty = sourceOptionsProperty.FindPropertyRelative("isActive");
			switch (sourceTypeValue)
			{
				case VectorSourceType.MapboxStreets:
				case VectorSourceType.MapboxStreetsWithBuildingIds:
					var sourcePropertyValue = MapboxDefaultVector.GetParameters(sourceTypeValue);
					layerSourceId.stringValue = sourcePropertyValue.Id;
					GUI.enabled = false;
					if (_isInitialized)
					{
						LoadEditorTileJSON(property, sourceTypeValue, layerSourceId.stringValue);
					}
					else
					{
						_isInitialized = true;
					}
					if (tileJSONData.PropertyDisplayNames.Count == 0 && tileJSONData.tileJSONLoaded)
					{
						EditorGUILayout.HelpBox("Invalid Map Id / There might be a problem with the internet connection.", MessageType.Error);
					}
					GUI.enabled = true;
					isActiveProperty.boolValue = true;
					break;
				case VectorSourceType.Custom:
					if (_isInitialized)
					{
						string test = layerSourceId.stringValue;
						LoadEditorTileJSON(property, sourceTypeValue, layerSourceId.stringValue);
					}
					else
					{
						_isInitialized = true;
					}
					if (tileJSONData.PropertyDisplayNames.Count == 0 && tileJSONData.tileJSONLoaded)
					{
						EditorGUILayout.HelpBox("Invalid Map Id / There might be a problem with the internet connection.", MessageType.Error);
					}
					isActiveProperty.boolValue = true;
					break;
				case VectorSourceType.None:
					isActiveProperty.boolValue = false;
					break;
				default:
					isActiveProperty.boolValue = false;
					break;
			}

			if (sourceTypeValue != VectorSourceType.None)
			{
				EditorGUILayout.LabelField(new GUIContent
				{
					text = "Map Features",
					tooltip = "Visualizers for vector features contained in a layer. "
				});

				var subLayerArray = property.FindPropertyRelative("vectorSubLayers");

				var layersRect = EditorGUILayout.GetControlRect(GUILayout.MinHeight(Mathf.Max(subLayerArray.arraySize + 1, 1) * _lineHeight),
																GUILayout.MaxHeight((subLayerArray.arraySize + 1) * _lineHeight));
				layerTreeView.Layers = subLayerArray;
				layerTreeView.Reload();
				layerTreeView.OnGUI(layersRect);

				selectedLayers = layerTreeView.GetSelection();

				//if there are selected elements, set the selection index at the first element.
				//if not, use the Selection index to persist the selection at the right index.
				if (selectedLayers.Count > 0)
				{
					//ensure that selectedLayers[0] isn't out of bounds
					if (selectedLayers[0] - layerTreeView.uniqueId > subLayerArray.arraySize - 1)
					{
						selectedLayers[0] = subLayerArray.arraySize - 1 + layerTreeView.uniqueId;
					}

					SelectionIndex = selectedLayers[0];
				}
				else
				{
					if (SelectionIndex > 0 && (SelectionIndex - layerTreeView.uniqueId <= subLayerArray.arraySize - 1))
					{
						selectedLayers = new int[1] { SelectionIndex };
						layerTreeView.SetSelection(selectedLayers);
					}
				}

				GUILayout.Space(EditorGUIUtility.singleLineHeight);

				EditorGUILayout.BeginHorizontal();
				var presetTypes = property.FindPropertyRelative("presetFeatureTypes");
				var names = Enum.GetNames(typeof(PresetFeatureType));
				GenericMenu menu = new GenericMenu();
				foreach(var name in names)
				{
					object parms = new object []{ name, property };
					menu.AddItem(new GUIContent() { text = name }, false, HandleMenuFunction, parms);
				}
				GUILayout.Space(0); // do not remove this line; it is needed for the next line to work
				Rect rect = GUILayoutUtility.GetLastRect();
				rect.y += 2*_lineHeight/3;

				if (EditorGUILayout.DropdownButton(new GUIContent { text = "Add Feature" }, FocusType.Passive, (GUIStyle)"minibuttonleft"))
				{
					menu.DropDown(rect);
				}

				if (GUILayout.Button(new GUIContent("Remove Selected"), (GUIStyle)"minibuttonright"))
				{
					foreach (var index in selectedLayers.OrderByDescending(i => i))
					{
						subLayerArray.DeleteArrayElementAtIndex(index - layerTreeView.uniqueId);
					}

					selectedLayers = new int[0];
					layerTreeView.SetSelection(selectedLayers);
				}

				EditorGUILayout.EndHorizontal();

				GUILayout.Space(EditorGUIUtility.singleLineHeight);
				Debug.Log(subLayerArray.arraySize);

				if (selectedLayers.Count == 1 && subLayerArray.arraySize != 0 && selectedLayers[0] - layerTreeView.uniqueId >= 0)
				{
					//ensure that selectedLayers[0] isn't out of bounds
					if (selectedLayers[0] - layerTreeView.uniqueId > subLayerArray.arraySize - 1)
					{
						selectedLayers[0] = subLayerArray.arraySize - 1 + layerTreeView.uniqueId;
					}

					SelectionIndex = selectedLayers[0];

					Debug.Log(SelectionIndex - layerTreeView.uniqueId);
					var layerProperty = subLayerArray.GetArrayElementAtIndex(SelectionIndex - layerTreeView.uniqueId);

					layerProperty.isExpanded = true;
					var subLayerCoreOptions = layerProperty.FindPropertyRelative("coreOptions");
					bool isLayerActive = subLayerCoreOptions.FindPropertyRelative("isActive").boolValue;
					if (!isLayerActive)
					{
						GUI.enabled = false;
					}

					DrawLayerVisualizerProperties(sourceTypeValue, layerProperty, property);
					if (!isLayerActive)
					{
						GUI.enabled = true;
					}
				}
				else
				{
					GUILayout.Label("Select a visualizer to see properties");
				}
			}
		}

		void HandleMenuFunction(object parms)
		{
			object[] parameters = (object[])parms;
			PresetFeatureType featureType = ((PresetFeatureType)Enum.Parse(typeof(PresetFeatureType), parameters[0].ToString()));
			var property = (SerializedProperty)parameters[1];
			var subLayerArray = property.FindPropertyRelative("vectorSubLayers");
			subLayerArray.arraySize++;

			var properties = PresetSubLayerPropertiesFetcher.GetSubLayerProperties(featureType);
			var subLayer = subLayerArray.GetArrayElementAtIndex(subLayerArray.arraySize - 1);

			subLayer.FindPropertyRelative("coreOptions.sublayerName").stringValue = properties.coreOptions.sublayerName;
			subLayer.FindPropertyRelative("presetFeatureType").enumValueIndex = (int)featureType;
			// Set defaults here because SerializedProperty copies the previous element.
			var subLayerCoreOptions = subLayer.FindPropertyRelative("coreOptions");
			CoreVectorLayerProperties coreOptions = properties.coreOptions;
			subLayerCoreOptions.FindPropertyRelative("isActive").boolValue = coreOptions.isActive;
			subLayerCoreOptions.FindPropertyRelative("layerName").stringValue = coreOptions.layerName;
			subLayerCoreOptions.FindPropertyRelative("geometryType").enumValueIndex = (int)coreOptions.geometryType;
			subLayerCoreOptions.FindPropertyRelative("snapToTerrain").boolValue = coreOptions.snapToTerrain;
			subLayerCoreOptions.FindPropertyRelative("combineMeshes").boolValue = coreOptions.combineMeshes;
			subLayerCoreOptions.FindPropertyRelative("lineWidth").floatValue = coreOptions.lineWidth;

			var subLayerExtrusionOptions = subLayer.FindPropertyRelative("extrusionOptions");
			var extrusionOptions = properties.extrusionOptions;
			subLayerExtrusionOptions.FindPropertyRelative("extrusionType").enumValueIndex = (int)extrusionOptions.extrusionType;
			subLayerExtrusionOptions.FindPropertyRelative("extrusionGeometryType").enumValueIndex = (int)extrusionOptions.extrusionGeometryType;
			subLayerExtrusionOptions.FindPropertyRelative("propertyName").stringValue = extrusionOptions.propertyName;
			subLayerExtrusionOptions.FindPropertyRelative("extrusionScaleFactor").floatValue = extrusionOptions.extrusionScaleFactor;

			var subLayerFilterOptions = subLayer.FindPropertyRelative("filterOptions");
			var filterOptions = properties.filterOptions;
			subLayerFilterOptions.FindPropertyRelative("filters").ClearArray();
			subLayerFilterOptions.FindPropertyRelative("combinerType").enumValueIndex = (int)filterOptions.combinerType;
			//Add any future filter related assignments here

			var subLayerGeometryMaterialOptions = subLayer.FindPropertyRelative("materialOptions");
			var materialOptions = properties.materialOptions;
			subLayerGeometryMaterialOptions.FindPropertyRelative("style").enumValueIndex = (int)materialOptions.style;

			GeometryMaterialOptions geometryMaterialOptionsReference = MapboxDefaultStyles.GetDefaultAssets();

			var mats = subLayerGeometryMaterialOptions.FindPropertyRelative("materials");
			mats.arraySize = 2;

			var topMatArray = mats.GetArrayElementAtIndex(0).FindPropertyRelative("Materials");
			var sideMatArray = mats.GetArrayElementAtIndex(1).FindPropertyRelative("Materials");

			if (topMatArray.arraySize == 0)
			{
				topMatArray.arraySize = 1;
			}
			if (sideMatArray.arraySize == 0)
			{
				sideMatArray.arraySize = 1;
			}

			var topMat = topMatArray.GetArrayElementAtIndex(0);
			var sideMat = sideMatArray.GetArrayElementAtIndex(0);

			var atlas = subLayerGeometryMaterialOptions.FindPropertyRelative("atlasInfo");
			var palette = subLayerGeometryMaterialOptions.FindPropertyRelative("colorPalette");

			topMat.objectReferenceValue = geometryMaterialOptionsReference.materials[0].Materials[0];
			sideMat.objectReferenceValue = geometryMaterialOptionsReference.materials[1].Materials[0];
			atlas.objectReferenceValue = geometryMaterialOptionsReference.atlasInfo;
			palette.objectReferenceValue = geometryMaterialOptionsReference.colorPalette;

			subLayer.FindPropertyRelative("buildingsWithUniqueIds").boolValue = properties.buildingsWithUniqueIds;
			subLayer.FindPropertyRelative("moveFeaturePositionTo").enumValueIndex = (int)properties.moveFeaturePositionTo;
			subLayer.FindPropertyRelative("MeshModifiers").ClearArray();
			subLayer.FindPropertyRelative("GoModifiers").ClearArray();

			var subLayerColliderOptions = subLayer.FindPropertyRelative("colliderOptions");
			subLayerColliderOptions.FindPropertyRelative("colliderType").enumValueIndex = (int)properties.colliderOptions.colliderType;

			selectedLayers = new int[1] { subLayerArray.arraySize - 1 + layerTreeView.uniqueId };
			layerTreeView.SetSelection(selectedLayers);
			EditorUtility.SetDirty(subLayerArray.serializedObject.targetObject);
		}


		void DrawLayerVisualizerProperties(VectorSourceType sourceType, SerializedProperty layerProperty, SerializedProperty property)
		{
			var subLayerCoreOptions = layerProperty.FindPropertyRelative("coreOptions");
			GUILayout.Space(-_lineHeight);
			visualizerNameAndType.normal.textColor = Color.white;
			visualizerNameAndType.fontStyle = FontStyle.Bold;
			EditorGUILayout.LabelField("Feature Name : "+ subLayerCoreOptions.FindPropertyRelative("sublayerName").stringValue, visualizerNameAndType);
			EditorGUILayout.LabelField("Type : " + "Building", visualizerNameAndType);
			EditorGUILayout.LabelField("Sub-type : " + "Highway", visualizerNameAndType);

			//*********************** LAYER NAME BEGINS ***********************************//
			VectorPrimitiveType primitiveTypeProp = (VectorPrimitiveType)subLayerCoreOptions.FindPropertyRelative("geometryType").enumValueIndex;

			var serializedMapObject = property.serializedObject;
			AbstractMap mapObject = (AbstractMap)serializedMapObject.targetObject;
			tileJsonData = mapObject.VectorData.LayerProperty.tileJsonData;

			var layerDisplayNames = tileJsonData.LayerDisplayNames;

			DrawLayerName(subLayerCoreOptions, layerDisplayNames);
			//*********************** LAYER NAME ENDS ***********************************//


			EditorGUI.indentLevel++;

			//*********************** FILTERS SECTION BEGINS ***********************************//
 			var filterOptions = layerProperty.FindPropertyRelative("filterOptions");
			filterOptions.FindPropertyRelative("_selectedLayerName").stringValue = subLayerCoreOptions.FindPropertyRelative("layerName").stringValue;
			GUILayout.Space(-_lineHeight);
			EditorGUILayout.PropertyField(filterOptions, new GUIContent("Filters"));
			//*********************** FILTERS SECTION ENDS ***********************************//



			//*********************** MODELING SECTION BEGINS ***********************************//
			EditorGUILayout.BeginVertical();
			_modelingSectionDrawer.DrawUI(subLayerCoreOptions, layerProperty, primitiveTypeProp, sourceType);
			//*********************** MODELING SECTION ENDS ***********************************//




			//*********************** TEXTURING SECTION BEGINS ***********************************//
			if (primitiveTypeProp != VectorPrimitiveType.Point && primitiveTypeProp != VectorPrimitiveType.Custom)
			{
				GUILayout.Space(-_lineHeight);
				EditorGUILayout.PropertyField(layerProperty.FindPropertyRelative("materialOptions"));
			}
			//*********************** TEXTURING SECTION ENDS ***********************************//


			//*********************** GAMEPLAY SECTION BEGINS ***********************************//
			_gameplaySectionDrawer.DrawUI(layerProperty,primitiveTypeProp);
			//*********************** GAMEPLAY SECTION ENDS ***********************************//

			EditorGUI.indentLevel--;
		}

		private void LoadEditorTileJSON(SerializedProperty property, VectorSourceType sourceTypeValue, string sourceString)
		{
			if (sourceTypeValue != VectorSourceType.None && !string.IsNullOrEmpty(sourceString))
			{
				if (tileJSONResponse == null || string.IsNullOrEmpty(sourceString) || sourceString != TilesetId)
				{
					//tileJSONData.ClearData();
					try
					{
						Unity.MapboxAccess.Instance.TileJSON.Get(sourceString, (response) =>
						{
							//if the code has reached this point it means that there is a valid access token
							tileJSONResponse = response;
							if (response == null || response.VectorLayers == null) //indicates bad tileresponse
							{
								tileJSONData.ClearData();
								return;
							}
							tileJSONData.ProcessTileJSONData(response);
						});
					}
					catch (System.Exception)
					{
						//no valid access token causes MapboxAccess to throw an error and hence setting this property
						tileJSONData.ClearData();
					}
				}
				else if (tileJSONData.LayerPropertyDescriptionDictionary.Count == 0)
				{
					tileJSONData.ProcessTileJSONData(tileJSONResponse);
				}
			}
			else
			{
				tileJSONData.ClearData();
			}
			TilesetId = sourceString;
		}

		public void DrawLayerName(SerializedProperty property, List<string> layerDisplayNames)
		{

			var layerNameLabel = new GUIContent
			{
				text = "Layer Name",
				tooltip = "The layer name from the Mapbox tileset that would be used for visualizing a feature"
			};

			//disable the selection if there is no layer
			if (layerDisplayNames.Count == 0)
			{
				EditorGUILayout.LabelField(layerNameLabel, new GUIContent("No layers found: Invalid MapId / No Internet."), (GUIStyle)"minipopUp");
				return;
			}

			//check the string value at the current _layerIndex to verify that the stored index matches the property string.
			var layerString = property.FindPropertyRelative("layerName").stringValue;
			if (layerDisplayNames.Contains(layerString))
			{
				//if the layer contains the current layerstring, set it's index to match
				_layerIndex = layerDisplayNames.FindIndex(s => s.Equals(layerString));

			}
			else
			{
				//if the selected layer isn't in the source, add a placeholder entry
				_layerIndex = 0;
				layerDisplayNames.Insert(0, layerString);
				if (!tileJsonData.LayerPropertyDescriptionDictionary.ContainsKey(layerString))
				{
					tileJsonData.LayerPropertyDescriptionDictionary.Add(layerString, new Dictionary<string, string>());
				}

			}

			//create the display name guicontent array with an additional entry for the currently selected item
			_layerTypeContent = new GUIContent[layerDisplayNames.Count];
			for (int extIdx = 0; extIdx < layerDisplayNames.Count; extIdx++)
			{
				_layerTypeContent[extIdx] = new GUIContent
				{
					text = layerDisplayNames[extIdx],
				};
			}

			//draw the layer selection popup
			_layerIndex = EditorGUILayout.Popup(layerNameLabel, _layerIndex, _layerTypeContent);
			var parsedString = layerDisplayNames.ToArray()[_layerIndex].Split(new string[] { tileJsonData.commonLayersKey }, System.StringSplitOptions.None)[0].Trim();
			property.FindPropertyRelative("layerName").stringValue = parsedString;
		}
	}
}
