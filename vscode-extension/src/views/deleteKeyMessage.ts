export interface DeleteKeyMessage {
  command: 'deleteKey';
  key: string;
  resourceGroup?: string;
}

/**
 * Builds the webview→extension Delete-Key message.
 *
 * resourceGroup is included only when provided (multi-group projects require it,
 * otherwise the backend returns 400; single-group projects omit it). Throws if the
 * key is blank so the UI can surface a validation message instead of silently
 * posting a no-op request.
 */
export function buildDeleteKeyMessage(key: string, resourceGroup: string | undefined): DeleteKeyMessage {
  const k = (key ?? '').trim();
  if (!k) {
    throw new Error('Key is required');
  }

  const msg: DeleteKeyMessage = { command: 'deleteKey', key: k };
  if (resourceGroup) {
    msg.resourceGroup = resourceGroup;
  }
  return msg;
}
