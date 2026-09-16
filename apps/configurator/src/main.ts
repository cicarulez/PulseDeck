import { bootstrapApplication } from '@angular/platform-browser';
import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeIt from '@angular/common/locales/it';
import { AppComponent } from './app/app.component';
registerLocaleData(localeIt);
bootstrapApplication(AppComponent, { providers: [{ provide: LOCALE_ID, useValue: 'it' }] }).catch(console.error);
