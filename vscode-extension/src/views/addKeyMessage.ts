export interface AddKeyMessage {
  command: 'addKey';
  key: string;
  resourceGroup?: string;
  values: { [language: string]: string };
}

/**
 * Builds the webview→extension Add-Key message. resourceGroup is included only
 * when provided (multi-group projects require it; single-group projects omit it).
 * Throws if the key name is blank so the UI can show a validation message.
 */
export function buildAddKeyMessage(
  keyName: string,
  keyValue: string,
  resourceGroup: string | undefined
): AddKeyMessage {
  const key = (keyName ?? '').trim();
  if (!key) {
    throw new Error('Key name is required');
  }
  const msg: AddKeyMessage = { command: 'addKey', key, values: { default: keyValue ?? '' } };
  if (resourceGroup) {
    msg.resourceGroup = resourceGroup;
  }
  return msg;
}
