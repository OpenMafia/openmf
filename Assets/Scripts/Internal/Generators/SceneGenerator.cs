﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MafiaUnity
{
    public class SceneGenerator : BaseGenerator
    {
        public MafiaFormats.Scene2BINLoader lastLoader;

        public override GameObject LoadObject(string path, Mission mission)
        {
            GameObject rootObject = LoadCachedObject(path);

            if (rootObject == null)
                rootObject = new GameObject(path);
            else
                return rootObject;

            Stream fs;

            try
            {
                fs = GameAPI.instance.fileSystem.GetStreamFromPath(path);
            }
            catch
            {
                return null;
            }

            using (var reader = new BinaryReader(fs))
            {
                var sceneLoader = new MafiaFormats.Scene2BINLoader();
                lastLoader = sceneLoader;

                sceneLoader.Load(reader);
                fs.Close();

                // TODO: Check if refs are null, clear then
                ModelGenerator.cachedTextures.Clear();

                var objects = new List<KeyValuePair<GameObject, MafiaFormats.Scene2BINLoader.Object>>();

                var backdrop = new GameObject("Backdrop sector");
                backdrop.transform.parent = rootObject.transform;
                StoreReference(mission, backdrop.name, backdrop);

                foreach (var obj in sceneLoader.objects)
                {
                    AddNewObject(objects, mission, obj);
                }

                // Handle special cases of objects loaded by external source
                foreach (var obj in sceneLoader.externalObjects)
                {
                    var go = BaseGenerator.FetchCacheReference(mission, obj.Value.name)?.gameObject;

                    if (go != null)
                    {
                        var specialObjDef = go.GetComponent<ObjectDefinition>();

                        if (specialObjDef == null)
                            specialObjDef = go.AddComponent<ObjectDefinition>();

                        specialObjDef.data = obj.Value;

                        specialObjDef.Init();
                    }
                    // Seems like object acts on its own, add it anyway
                    else AddNewObject(objects, mission, obj);
                }

                var primary = FetchReference(mission, "Primary sector");

                if (primary == null)
                    primary = new GameObject("Primary Sector");

                var objDef = primary.AddComponent<ObjectDefinition>();
                var dummySectorData = new MafiaFormats.Scene2BINLoader.Object();
                dummySectorData.type = MafiaFormats.Scene2BINLoader.ObjectType.Sector;
                objDef.data = dummySectorData;
                primary.transform.parent = rootObject.transform;

                objects = objects.OrderByDescending(x => x.Value.isPatch).ToList();

                foreach (var obj in objects)
                {
                    var newObject = obj.Key;

                    if (obj.Value.isPatch)
                    {
                        var searchName = obj.Value.name;
                        var redefObject = FindParent(mission, searchName, null);

                        if (redefObject != null)
                        {
                            if (obj.Value.isParentPatched && obj.Value.parentName != null)
                            {
                                var parent = FindParent(mission, obj.Value.parentName);

                                if (parent != null)
                                    redefObject.transform.parent = parent.transform;
                            }

                            if (obj.Value.isPositionPatched)
                                redefObject.transform.localPosition = obj.Value.pos;

                            /* if (obj.Value.isPosition2Patched)
                                redefObject.transform.position = obj.Value.pos2; */

                            if (obj.Value.isRotationPatched)
                                redefObject.transform.localRotation = obj.Value.rot;

                            if (obj.Value.isScalePatched)
                                redefObject.transform.localScale = obj.Value.scale;

                            redefObject.SetActive(!obj.Value.isHidden);

                            GameObject.DestroyImmediate(newObject, true);
                            continue;
                        }
                    }

                    newObject.transform.parent = FindParent(mission, obj.Value.parentName, rootObject).transform;

                    newObject.transform.localPosition = obj.Value.pos;
                    newObject.transform.localRotation = obj.Value.rot;
                    newObject.transform.localScale = obj.Value.scale;

                    newObject.SetActive(!obj.Value.isHidden);

                    var specObject = newObject.AddComponent<ObjectDefinition>();
                    specObject.data = obj.Value;
                    specObject.Init();
                }
            }

            // NOTE(zaklaus): Hardcode 'Primary sector' scale to (1,1,1)
            var primarySector = GameObject.Find("Primary sector");

            if (primarySector != null)
                primarySector.transform.localScale = new Vector3(1,1,1);

            //StoreChachedObject(path, rootObject);

            return rootObject;
        }

        private void AddNewObject(List<KeyValuePair<GameObject, MafiaFormats.Scene2BINLoader.Object>> objects, Mission mission, KeyValuePair<string, MafiaFormats.Scene2BINLoader.Object> obj)
        {
            GameObject newObject;

            if (obj.Value.modelName == null || (obj.Value.type != MafiaFormats.Scene2BINLoader.ObjectType.Model && obj.Value.specialType == 0))
                newObject = new GameObject();
            else
                newObject = GameAPI.instance.modelGenerator.LoadObject(Path.Combine("models", obj.Value.modelName), null);

            if (newObject == null)
                return;

            newObject.name = obj.Value.name;

            StoreReference(mission, newObject.name, newObject);

            newObject.transform.localPosition = obj.Value.pos;
            newObject.transform.localRotation = obj.Value.rot;
            newObject.transform.localScale = obj.Value.scale;

            objects.Add(new KeyValuePair<GameObject, MafiaFormats.Scene2BINLoader.Object>(newObject, obj.Value));
        }

        GameObject FindParent(Mission mission, string name, GameObject defaultObject=null)
        {
            if (name != null)
            {
                var path = name.Split('.');
                GameObject parentObject = FetchReference(mission, path[0]);

                if (parentObject != null && path.Length == 2)
                {
                    var parent = parentObject.transform.FindDeepChild(path[1]);

                    if (parent != null)
                    {
                        parentObject = parent.gameObject;
                    }
                }


                if (parentObject != null)
                    return parentObject;
                else
                    return defaultObject;
            }
            else
                return defaultObject;
        }
    }
}
