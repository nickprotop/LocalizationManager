// LRM Cloud Application JavaScript

// =============================================================================
// Keyboard Shortcuts
// =============================================================================
window.lrmKeyboard = {
    dotNetRef: null,

    init: function(dotNetReference) {
        this.dotNetRef = dotNetReference;
        document.addEventListener('keydown', this.handleKeyDown.bind(this));
    },

    dispose: function() {
        document.removeEventListener('keydown', this.handleKeyDown.bind(this));
        this.dotNetRef = null;
    },

    handleKeyDown: function(e) {
        if (!this.dotNetRef) return;

        // Don't capture shortcuts when typing in input fields (unless it's a global shortcut)
        const isInputFocused = document.activeElement &&
            (document.activeElement.tagName === 'INPUT' ||
             document.activeElement.tagName === 'TEXTAREA' ||
             document.activeElement.isContentEditable);

        // Ctrl+S - Save
        if (e.ctrlKey && e.key === 's') {
            e.preventDefault();
            this.dotNetRef.invokeMethodAsync('OnKeyboardSave');
            return;
        }

        // Ctrl+F - Focus search (only when not in input)
        if (e.ctrlKey && e.key === 'f' && !isInputFocused) {
            e.preventDefault();
            this.dotNetRef.invokeMethodAsync('OnKeyboardSearch');
            return;
        }

        // Escape - Close drawer/dialog
        if (e.key === 'Escape') {
            this.dotNetRef.invokeMethodAsync('OnKeyboardEscape');
            return;
        }

        // Delete - Delete selected (only when not in input)
        if (e.key === 'Delete' && !isInputFocused) {
            this.dotNetRef.invokeMethodAsync('OnKeyboardDelete');
            return;
        }
    },

    focusElement: function(selector) {
        const element = document.querySelector(selector);
        if (element) {
            element.focus();
        }
    }
};

// =============================================================================
// Offline Detection
// =============================================================================
window.lrmOffline = {
    dotNetRef: null,

    init: function(dotNetReference) {
        this.dotNetRef = dotNetReference;

        // Set initial state
        this.notifyStatusChange();

        // Listen for online/offline events
        window.addEventListener('online', () => this.notifyStatusChange());
        window.addEventListener('offline', () => this.notifyStatusChange());
    },

    dispose: function() {
        window.removeEventListener('online', () => this.notifyStatusChange());
        window.removeEventListener('offline', () => this.notifyStatusChange());
        this.dotNetRef = null;
    },

    notifyStatusChange: function() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnOnlineStatusChanged', navigator.onLine);
        }
    },

    checkConnection: async function() {
        try {
            // Try to fetch a small resource to verify actual connectivity
            const response = await fetch('/api/health', {
                method: 'HEAD',
                cache: 'no-store'
            });
            return response.ok;
        } catch {
            return navigator.onLine;
        }
    }
};

// =============================================================================
// Unsaved Changes Warning
// =============================================================================
window.lrmUnsavedChanges = {
    hasUnsavedChanges: false,

    setHasUnsavedChanges: function(value) {
        this.hasUnsavedChanges = value;
    },

    init: function() {
        window.addEventListener('beforeunload', (e) => {
            if (this.hasUnsavedChanges) {
                e.preventDefault();
                e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return e.returnValue;
            }
        });
    }
};

// Initialize unsaved changes warning on load
window.lrmUnsavedChanges.init();

// =============================================================================
// Clipboard Operations
// =============================================================================
window.lrmClipboard = {
    copyText: async function(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.error('Failed to copy text:', err);
            return false;
        }
    }
};

// =============================================================================
// Focus Management
// =============================================================================
window.lrmFocus = {
    trapFocus: function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const focusableElements = element.querySelectorAll(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );

        if (focusableElements.length === 0) return;

        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];

        element.addEventListener('keydown', (e) => {
            if (e.key !== 'Tab') return;

            if (e.shiftKey && document.activeElement === firstElement) {
                e.preventDefault();
                lastElement.focus();
            } else if (!e.shiftKey && document.activeElement === lastElement) {
                e.preventDefault();
                firstElement.focus();
            }
        });

        firstElement.focus();
    },

    focusElement: function(selector) {
        const element = document.querySelector(selector);
        if (element) {
            element.focus();
        }
    }
};

// =============================================================================
// Theme Persistence
// =============================================================================
window.lrmTheme = {
    getPreference: function() {
        const stored = localStorage.getItem('lrm-theme');
        if (stored) return stored;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    setPreference: function(theme) {
        localStorage.setItem('lrm-theme', theme);
    },

    watchSystemTheme: function(dotNetRef) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (!localStorage.getItem('lrm-theme')) {
                dotNetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches);
            }
        });
    }
};

// =============================================================================
// Service Worker Update Detection
// =============================================================================
window.lrmServiceWorker = {
    dotNetRef: null,
    registration: null,

    init: async function(dotNetReference) {
        this.dotNetRef = dotNetReference;
        if (!('serviceWorker' in navigator)) return;

        try {
            this.registration = await navigator.serviceWorker.register('/app/service-worker.js');
            console.log('[SW] Registered, scope:', this.registration.scope);

            // Only check for waiting worker if there's already an active controller
            // This prevents showing the banner on first visit or when there's no real update
            if (this.registration.waiting && navigator.serviceWorker.controller) {
                console.log('[SW] Update waiting - notify user');
                this.notifyUpdateAvailable();
            }

            // Listen for new service worker installing
            this.registration.addEventListener('updatefound', () => {
                const newWorker = this.registration.installing;
                if (newWorker) {
                    newWorker.addEventListener('statechange', () => {
                        // Only notify if there's a controlling worker (meaning this is an update, not first install)
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            console.log('[SW] New version installed - notify user');
                            this.notifyUpdateAvailable();
                        }
                    });
                }
            });

            // Reload when new SW takes control
            navigator.serviceWorker.addEventListener('controllerchange', () => {
                window.location.reload();
            });

            // Check for updates periodically (every 60 seconds) - only in production
            if (!window.location.hostname.includes('localhost')) {
                setInterval(() => this.registration?.update(), 60000);
            }

        } catch (error) {
            console.error('[SW] Registration failed:', error);
        }
    },

    dispose: function() {
        this.dotNetRef = null;
    },

    notifyUpdateAvailable: function() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnUpdateAvailable');
        }
    },

    applyUpdate: function() {
        if (this.registration?.waiting) {
            this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
            // Fallback reload in case controllerchange doesn't fire
            setTimeout(() => window.location.reload(), 1000);
        }
    }
};
