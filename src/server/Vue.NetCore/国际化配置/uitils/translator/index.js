// import Vue from 'vue'
import store from '../../store'
// import en from '../../locale/en.json'
import { currentLang } from '@/components/lang'
var _currentLang = currentLang();
let app;

function $ts(key, toLower) {//是否将后面的所有文字转小写
	if (key === undefined || key === null) {
		return '';
	}
	if (Array.isArray(key)) {
		return ts_arr(key, toLower);
	}
	if (_currentLang == "zh-cn") {
		return key;
	}

	return store.getters.local()[key] || key;
}
//按模板翻译，如：'值只能在[{$ts}]'
function $tst(key, templateVal) {
	if (key === undefined || key === null) {
		return '';
	}
	//替换模板
	return $ts(key).replace('{$ts}', templateVal);
}
let ts_arr = function (arr, toLower) {
	if (_currentLang == "zh-cn") return arr.join('');
	let ts_string = '';
	for (let index = 0; index < arr.length; index++) {
		//toLower除了第一个，其他转小写
		if (toLower) {
			ts_string = ts_string + " " + ((store.getters.local()[arr[index]] || arr[index]).toLocaleLowerCase());
		} else {
			ts_string = ts_string + " " + (store.getters.local()[arr[index]] || arr[index]);
		}
	}
	return ts_string;
};
function $loadSource() {
	let langType = localStorage.getItem(lang_storage_key);
	if (!langType) {
		// if (navigator.language == "zh-cn") {
		langType = "zh-cn";
		// } else {
		// 	langType = "en";
		// }
	}

	$changeSource(langType, true);
}

function $changeSource(langType, isInit, source) {
	if (_currentLang == langType && !isInit) {
		return;
	}
	_currentLang = langType;
	localStorage.setItem(lang_storage_key, langType);

	app.config.globalProperties.isCN = _currentLang == 'zh-cn';
	//store.commit("setLocal", source);
}
//其他语言增加对应的js文件：'zh-tw', 'en', 'fr', 'es', 'ru', 'ar', 'vi'
import en from './en.js'
function loadLangScript() {
	if (_currentLang == 'zh-cn') {
		return
	}
	window.global_lang = {}
	store.commit("setLocal", en);
}
export default {
	init(_app) {
		loadLangScript();
		app = _app;
		app.config.globalProperties.$ts = $ts;
		app.config.globalProperties.$tst = $tst;
		$loadSource();
		app.config.globalProperties.$changeSource = $changeSource;
	}

}
