﻿/*
* Tencent is pleased to support the open source community by making Puerts available.
* Copyright (C) 2020 THL A29 Limited, a Tencent company.  All rights reserved.
* Puerts is licensed under the BSD 3-Clause License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
* This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
*/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Puerts
{
    internal class StaticCallbacks
    {
        [MonoPInvokeCallback(typeof(ModuleResolveCallback))]
        internal static IntPtr ModuleResolverCallback(string identifer, int jsEnvIdx, ref int byteLength)
        {
            JsEnv env = JsEnv.jsEnvs[jsEnvIdx];
            byte[] content;
            try
            {
                content = env.ResolveModuleContent(identifer);
                if (content == null)
                {
                    content = Encoding.UTF8.GetBytes("throw new Error(`ExecuteModule '" + identifer + "' with null content`)");
                }
            }
            catch (Exception e)
            {
                content = Encoding.UTF8.GetBytes("throw new Error(`resolve module '" + identifer + "' error: " + e.Message + "`)");
            }

            byteLength = content.Length;

            // 这段内存是没有末尾0的，如果这段真是字符串，qjs可能需要添加末尾0才能准确解析，所以多分配一位。
            // 在我们的C++侧分配内存，不使用AllocHGlobal
            IntPtr ptr = PuertsDLL.AllocCharMemory(byteLength + 1); 

            Marshal.Copy(content, 0, ptr, byteLength);
            return ptr;
        }

        [MonoPInvokeCallback(typeof(PushJSFunctionArgumentsCallback))]
        internal static void PushJSFunctionArgumentsCallback(IntPtr isolate, int jsEnvIdx, IntPtr nativeJsFuncPtr)
        {
            try 
            {
                if (JsEnv.jsEnvs[jsEnvIdx].ArgumentsPusher == null)
                {
                    throw new Exception("JsEnv.JSFunctionArgumentsPusher is not setted");
                }
                JsEnv.jsEnvs[jsEnvIdx].ArgumentsPusher(isolate, jsEnvIdx, nativeJsFuncPtr);
            } 
            catch(Exception e)
            {
                PuertsDLL.ThrowException(isolate, "JsEnvCallbackWrap c# exception:" + e.Message + ",stack:" + e.StackTrace);
            }
        }

        [MonoPInvokeCallback(typeof(V8FunctionCallback))]
        internal static void JsEnvCallbackWrap(IntPtr isolate, IntPtr info, IntPtr self, int paramLen, long data)
        {
            try
            {
                int jsEnvIdx, callbackIdx;
                Utils.LongToTwoInt(data, out jsEnvIdx, out callbackIdx);
                JsEnv.jsEnvs[jsEnvIdx].InvokeCallback(isolate, callbackIdx, info, self, paramLen);
            }
            catch (Exception e)
            {
                PuertsDLL.ThrowException(isolate, "JsEnvCallbackWrap c# exception:" + e.Message + ",stack:" + e.StackTrace);
            }
        }

        [MonoPInvokeCallback(typeof(V8DestructorCallback))]
        internal static void GeneralDestructor(IntPtr self, long data)
        {
            try
            {
                int jsEnvIdx, callbackIdx;
                Utils.LongToTwoInt(data, out jsEnvIdx, out callbackIdx);
                JsEnv.jsEnvs[jsEnvIdx].JsReleaseObject(self.ToInt32());
            }
            catch {}
        }

        [MonoPInvokeCallback(typeof(V8ConstructorCallback))]
        internal static IntPtr ConstructorWrap(IntPtr isolate, IntPtr info, int paramLen, long data)
        {
            try
            {
                int jsEnvIdx, callbackIdx;
                Utils.LongToTwoInt(data, out jsEnvIdx, out callbackIdx);
                var ret = JsEnv.jsEnvs[jsEnvIdx].InvokeConstructor(isolate, callbackIdx, info, paramLen);
                return ret;
            }
            catch (Exception e)
            {
                PuertsDLL.ThrowException(isolate, "ConstructorWrap c# exception:" + e.Message + ",stack:" + e.StackTrace);
                return IntPtr.Zero;
            }
        }

        [MonoPInvokeCallback(typeof(V8FunctionCallback))]
        internal static void ReturnTrue(IntPtr isolate, IntPtr info, IntPtr self, int paramLen, long data)
        {
            PuertsDLL.ReturnBoolean(isolate, info, true);
        }
    }
}