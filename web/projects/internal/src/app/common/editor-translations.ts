import { TranslationDictionary } from '@dynamic-cube/shared';

/**
 * Editor texts other screens share with the workflow designer and the service builder (whose full texts load with them):
 * undo, redo, full window, and the links that open them.
 */
export const EDITOR_TRANSLATIONS: TranslationDictionary = {
  ar: {
    'designer.open': 'فتح المصمم',
    'designer.openFrom': 'فتح في المصمم (إصدار جديد من هذا)',
    'designer.undo': 'تراجع (Ctrl+Z)',
    'designer.redo': 'إعادة (Ctrl+Y)',
    'designer.fullWindow': 'ملء النافذة',
    'designer.exitFullWindow': 'الخروج من ملء النافذة (Esc)',
    'designer.delete': 'حذف المحدد (Delete)',
    'builder.open': 'فتح منشئ الخدمات',
    'builder.openFrom': 'فتح في منشئ الخدمات (إصدار جديد من هذا)',
  },
  en: {
    'designer.open': 'Open the designer',
    'designer.openFrom': 'Open in the designer (a new version from this one)',
    'designer.undo': 'Undo (Ctrl+Z)',
    'designer.redo': 'Redo (Ctrl+Y)',
    'designer.fullWindow': 'Full window',
    'designer.exitFullWindow': 'Leave full window (Esc)',
    'designer.delete': 'Delete selection (Delete)',
    'builder.open': 'Open the service builder',
    'builder.openFrom': 'Open in the service builder (a new version from this one)',
  },
};
