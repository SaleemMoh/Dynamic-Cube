import { InjectionToken, Type } from '@angular/core';

/**
 * The component that edits one row of a rows field in a dialog. It is the shared dynamic form inside a dialog, which
 * itself shows rows fields, so the rows field gets it through this token instead of importing it (no import cycle).
 */
export const ROW_DIALOG = new InjectionToken<Type<unknown>>('ROW_DIALOG');
