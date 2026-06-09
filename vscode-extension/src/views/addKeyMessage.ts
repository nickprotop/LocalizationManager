export interface AddKeyMessage {
  command: 'addKey';
  key: string;
  resourceGroup?: string;
  values: { [language: string]: string };
}

/**
 * Per-language value entered in the Add-Key dialog. The default language uses the
 * sentinel code 'default' (matched server-side to the suffix-less default file
 * regardless of the configured DefaultLanguageCode).
 */
export interface AddKeyInput {
  keyName: string;
  resourceGroup: string | undefined;
  /** Map of language code -> value. The default language is keyed 'default'. */
  values: { [language: string]: string };
}

/**
 * Builds the webview→extension Add-Key message from a per-language value map.
 *
 * resourceGroup is included only when provided (multi-group projects require it;
 * single-group projects omit it). Blank values are preserved (an empty default
 * value is allowed but the caller is expected to have warned the user). Throws if
 * the key name is blank so the UI can show a validation message.
 */
export function buildAddKeyMessage(input: AddKeyInput): AddKeyMessage {
  const key = (input.keyName ?? '').trim();
  if (!key) {
    throw new Error('Key name is required');
  }

  // Normalize values: trim nothing (values may legitimately contain whitespace),
  // but coerce undefined/null to empty string so every requested column is sent.
  const values: { [language: string]: string } = {};
  for (const [lang, value] of Object.entries(input.values ?? {})) {
    values[lang] = value ?? '';
  }
  // Always ensure a default entry exists so single-language projects still work.
  if (!('default' in values)) {
    values.default = '';
  }

  const msg: AddKeyMessage = { command: 'addKey', key, values };
  if (input.resourceGroup) {
    msg.resourceGroup = input.resourceGroup;
  }
  return msg;
}

/**
 * Returns true when the default-language value is blank. The Add-Key dialog uses
 * this to warn before submitting (mirrors the empty-value warning shown for
 * culture files in the grid).
 */
export function isDefaultValueEmpty(values: { [language: string]: string }): boolean {
  const v = values?.default;
  return v === undefined || v === null || v.trim() === '';
}
