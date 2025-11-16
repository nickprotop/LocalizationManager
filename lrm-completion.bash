#!/usr/bin/env bash
# Bash completion script for lrm (Localization Resource Manager)
#
# Installation:
#   1. Copy this file to /etc/bash_completion.d/lrm or ~/.local/share/bash-completion/completions/lrm
#   2. Or source it in your ~/.bashrc: source /path/to/lrm-completion.bash
#   3. Restart your shell or run: source ~/.bashrc

_lrm_completions() {
    local cur prev opts base
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"

    # Main commands
    local commands="validate stats view add update delete merge-duplicates export import edit translate config scan check list-languages add-language remove-language backup"

    # Global options
    local global_opts="--path -p --help -h --version -v"

    # Command-specific options
    local validate_opts="--path -p --format --missing-only --placeholder-types --no-placeholder-validation --help -h"
    local stats_opts="--path -p --format --help -h"
    local view_opts="--path -p --show-comments --format --regex --sort --no-limit --help -h"
    local add_opts="--path -p --lang -l --comment --no-backup --help -h"
    local update_opts="--path -p --lang -l --comment --interactive -i --yes -y --no-backup --help -h"
    local delete_opts="--path -p --yes -y --no-backup --all-duplicates --help -h"
    local merge_duplicates_opts="--path -p --all --auto-first --yes -y --no-backup --help -h"
    local export_opts="--path -p --output -o --format --include-status --help -h"
    local import_opts="--path -p --overwrite --no-backup --help -h"
    local edit_opts="--path -p --help -h"
    local translate_opts="--path -p --provider --target-languages --batch-size --only-missing --overwrite --dry-run --no-cache --no-backup --source-language --format --config-file --help -h"
    local config_opts="set-api-key get-api-key delete-api-key list-providers --help -h"
    local scan_opts="--path -p --source-path --exclude --strict --show-unused --show-missing --show-references --resource-classes --localization-methods --format --help -h"
    local check_opts="--path -p --source-path --exclude --strict --format --help -h"
    local list_languages_opts="--path -p --format --help -h"
    local add_language_opts="--path -p --culture -c --base-name --copy-from --empty --yes -y --help -h"
    local remove_language_opts="--path -p --culture -c --base-name --yes -y --no-backup --help -h"

    # Config subcommands
    local config_set_api_key_opts="--provider -p --key -k --help -h"
    local config_get_api_key_opts="--provider --help -h"
    local config_delete_api_key_opts="--provider -p --help -h"
    local config_list_providers_opts="--help -h"

    # Backup subcommands
    local backup_opts="list create restore diff info prune --help -h"
    local backup_list_opts="--path -p --file --all --limit --show-details --help -h"
    local backup_create_opts="--path -p --file --all --operation --help -h"
    local backup_restore_opts="--path -p --version --keys --preview --yes -y --no-backup --help -h"
    local backup_diff_opts="--path -p --from --to --output --show-unchanged --format --help -h"
    local backup_info_opts="--path -p --help -h"
    local backup_prune_opts="--path -p --file --all --version --older-than --keep --dry-run --yes -y --help -h"

    # Format options
    local format_opts="table json simple csv tui"

    # Translation providers
    local provider_opts="google deepl libretranslate ollama openai claude azureopenai azuretranslator"

    # Get the command and subcommand (first and second non-option words)
    local command=""
    local subcommand=""
    local cmd_count=0
    for ((i=1; i<COMP_CWORD; i++)); do
        if [[ "${COMP_WORDS[i]}" != -* ]]; then
            if [[ $cmd_count -eq 0 ]]; then
                command="${COMP_WORDS[i]}"
                cmd_count=1
            elif [[ $cmd_count -eq 1 ]]; then
                subcommand="${COMP_WORDS[i]}"
                break
            fi
        fi
    done

    # Complete based on previous word
    case "${prev}" in
        --path|-p)
            # Complete directories
            COMPREPLY=( $(compgen -d -- "${cur}") )
            return 0
            ;;
        --format)
            # Complete format options
            COMPREPLY=( $(compgen -W "${format_opts}" -- "${cur}") )
            return 0
            ;;
        --provider)
            # Complete translation providers
            COMPREPLY=( $(compgen -W "${provider_opts}" -- "${cur}") )
            return 0
            ;;
        --output|-o|--config-file)
            # Complete files for output
            COMPREPLY=( $(compgen -f -- "${cur}") )
            return 0
            ;;
        --target-languages)
            # Suggest language codes (common ones)
            COMPREPLY=( $(compgen -W "en es fr de it pt ja zh ko ru ar" -- "${cur}") )
            return 0
            ;;
        --batch-size)
            # Suggest batch sizes
            COMPREPLY=( $(compgen -W "5 10 20 50 100" -- "${cur}") )
            return 0
            ;;
        --source-path)
            # Complete directories for source path
            COMPREPLY=( $(compgen -d -- "${cur}") )
            return 0
            ;;
        --exclude)
            # Suggest common exclude patterns
            COMPREPLY=( $(compgen -W "**/*.g.cs **/bin/** **/obj/** **/node_modules/**" -- "${cur}") )
            return 0
            ;;
        --resource-classes|--localization-methods)
            # Suggest common values (comma-separated)
            COMPREPLY=( $(compgen -W "Resources Strings AppResources GetString Translate L T" -- "${cur}") )
            return 0
            ;;
        --culture|-c|--copy-from)
            # Suggest language codes
            COMPREPLY=( $(compgen -W "en es fr de it pt ja zh ko ru ar el tr nl pl cs sv" -- "${cur}") )
            return 0
            ;;
        --base-name)
            # No completion for base name
            COMPREPLY=()
            return 0
            ;;
        --key|-k)
            # No completion for API keys (security)
            COMPREPLY=()
            return 0
            ;;
        --file)
            # Complete .resx files
            COMPREPLY=( $(compgen -f -X '!*.resx' -- "${cur}") )
            return 0
            ;;
        --version|--from|--to)
            # Version numbers - no completion
            COMPREPLY=()
            return 0
            ;;
        --limit)
            # Suggest common limits
            COMPREPLY=( $(compgen -W "10 20 50 100 0" -- "${cur}") )
            return 0
            ;;
        --operation|--keys)
            # No completion for these
            COMPREPLY=()
            return 0
            ;;
        --placeholder-types)
            # Complete placeholder types (comma-separated)
            COMPREPLY=( $(compgen -W "dotnet printf icu template all" -- "${cur}") )
            return 0
            ;;
        lrm)
            # Complete main commands
            COMPREPLY=( $(compgen -W "${commands} ${global_opts}" -- "${cur}") )
            return 0
            ;;
    esac

    # Complete based on current command
    if [[ -z "${command}" ]]; then
        # No command yet, suggest commands
        COMPREPLY=( $(compgen -W "${commands} ${global_opts}" -- "${cur}") )
        return 0
    fi

    case "${command}" in
        validate)
            COMPREPLY=( $(compgen -W "${validate_opts}" -- "${cur}") )
            ;;
        stats)
            COMPREPLY=( $(compgen -W "${stats_opts}" -- "${cur}") )
            ;;
        view)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${view_opts}" -- "${cur}") )
            else
                # Key name argument
                COMPREPLY=()
            fi
            ;;
        add)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${add_opts}" -- "${cur}") )
            else
                # Key name argument
                COMPREPLY=()
            fi
            ;;
        update)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${update_opts}" -- "${cur}") )
            else
                # Key name argument
                COMPREPLY=()
            fi
            ;;
        delete)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${delete_opts}" -- "${cur}") )
            else
                # Key name argument
                COMPREPLY=()
            fi
            ;;
        merge-duplicates)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${merge_duplicates_opts}" -- "${cur}") )
            else
                # Key name argument (optional with --all)
                COMPREPLY=()
            fi
            ;;
        export)
            COMPREPLY=( $(compgen -W "${export_opts}" -- "${cur}") )
            ;;
        import)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${import_opts}" -- "${cur}") )
            else
                # Complete CSV files
                COMPREPLY=( $(compgen -f -X '!*.csv' -- "${cur}") )
            fi
            ;;
        edit)
            COMPREPLY=( $(compgen -W "${edit_opts}" -- "${cur}") )
            ;;
        translate)
            COMPREPLY=( $(compgen -W "${translate_opts}" -- "${cur}") )
            ;;
        scan)
            COMPREPLY=( $(compgen -W "${scan_opts}" -- "${cur}") )
            ;;
        check)
            COMPREPLY=( $(compgen -W "${check_opts}" -- "${cur}") )
            ;;
        list-languages)
            COMPREPLY=( $(compgen -W "${list_languages_opts}" -- "${cur}") )
            ;;
        add-language)
            COMPREPLY=( $(compgen -W "${add_language_opts}" -- "${cur}") )
            ;;
        remove-language)
            COMPREPLY=( $(compgen -W "${remove_language_opts}" -- "${cur}") )
            ;;
        config)
            # Handle config subcommands
            if [[ -z "${subcommand}" ]]; then
                # No subcommand yet, suggest subcommands
                COMPREPLY=( $(compgen -W "${config_opts}" -- "${cur}") )
            else
                # Complete options for the specific subcommand
                case "${subcommand}" in
                    set-api-key)
                        COMPREPLY=( $(compgen -W "${config_set_api_key_opts}" -- "${cur}") )
                        ;;
                    get-api-key)
                        COMPREPLY=( $(compgen -W "${config_get_api_key_opts}" -- "${cur}") )
                        ;;
                    delete-api-key)
                        COMPREPLY=( $(compgen -W "${config_delete_api_key_opts}" -- "${cur}") )
                        ;;
                    list-providers)
                        COMPREPLY=( $(compgen -W "${config_list_providers_opts}" -- "${cur}") )
                        ;;
                    *)
                        COMPREPLY=()
                        ;;
                esac
            fi
            ;;
        backup)
            # Handle backup subcommands
            if [[ -z "${subcommand}" ]]; then
                # No subcommand yet, suggest subcommands
                COMPREPLY=( $(compgen -W "${backup_opts}" -- "${cur}") )
            else
                # Complete options for the specific subcommand
                case "${subcommand}" in
                    list)
                        COMPREPLY=( $(compgen -W "${backup_list_opts}" -- "${cur}") )
                        ;;
                    create)
                        COMPREPLY=( $(compgen -W "${backup_create_opts}" -- "${cur}") )
                        ;;
                    restore)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${backup_restore_opts}" -- "${cur}") )
                        else
                            # Complete .resx files
                            COMPREPLY=( $(compgen -f -X '!*.resx' -- "${cur}") )
                        fi
                        ;;
                    diff)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${backup_diff_opts}" -- "${cur}") )
                        else
                            # Complete .resx files
                            COMPREPLY=( $(compgen -f -X '!*.resx' -- "${cur}") )
                        fi
                        ;;
                    info)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${backup_info_opts}" -- "${cur}") )
                        else
                            # Complete .resx files or version numbers
                            COMPREPLY=( $(compgen -f -X '!*.resx' -- "${cur}") )
                        fi
                        ;;
                    prune)
                        COMPREPLY=( $(compgen -W "${backup_prune_opts}" -- "${cur}") )
                        ;;
                    *)
                        COMPREPLY=()
                        ;;
                esac
            fi
            ;;
        *)
            COMPREPLY=()
            ;;
    esac
}

# Register the completion function
complete -F _lrm_completions lrm
