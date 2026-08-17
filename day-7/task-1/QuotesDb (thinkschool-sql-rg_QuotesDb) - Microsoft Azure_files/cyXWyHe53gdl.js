define("use-sync-external-store/shim/with-selector",["exports","react","use-sync-external-store/shim"],function(e){
/**
 * @license React
 * use-sync-external-store-shim/with-selector.production.js
 *
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */
"use strict";var r=require("react"),n=require("use-sync-external-store/shim");var u="function"==typeof Object.is?Object.is:function(e,r){return e===r&&(0!==e||1/e==1/r)||e!=e&&r!=r},t=n.useSyncExternalStore,i=r.useRef,s=r.useEffect,a=r.useMemo,c=r.useDebugValue;e.useSyncExternalStoreWithSelector=function(e,r,n,l,o){var f=i(null);if(null===f.current){var v={hasValue:!1,value:null};f.current=v}else v=f.current;f=a(function(){function e(e){if(!s){if(s=!0,t=e,e=l(e),void 0!==o&&v.hasValue){var r=v.value;if(o(r,e))return i=r}return i=e}if(r=i,u(t,e))return r;var n=l(e);return void 0!==o&&o(r,n)?(t=e,r):(t=e,i=n)}var t,i,s=!1,a=void 0===n?null:n;return[function(){return e(r())},null===a?void 0:function(){return e(a())}]},[r,n,l,o]);var h=t(e,f[0],f[1]);return s(function(){v.hasValue=!0,v.value=h},[h]),c(h),h}});