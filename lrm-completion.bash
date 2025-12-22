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
    local commands="init convert validate stats view add update delete merge-duplicates export import edit translate config scan check chain list-languages add-language remove-language backup web cloud"

    # Global options
    local global_opts="--path -p --backend --help -h --version -v"

    # Command-specific options
    local init_opts="--path -p --interactive -i --format --default-lang --languages --base-name --yes -y --help -h"
    local convert_opts="--path -p --from --to --output -o --nested --include-comments --no-backup --yes -y --help -h"
    local validate_opts="--path -p --format --placeholder-types --no-placeholder-validation --no-scan-code --source-path --help -h"
    local stats_opts="--path -p --format --help -h"
    local view_opts="--path -p --show-comments --format --regex --sort --no-limit --case-sensitive --search-in --count --status --not --cultures --keys-only --help -h"
    local add_opts="--path -p --lang -l --comment --no-backup --ask-missing --plural --plural-form --interactive -i --help -h"
    local update_opts="--path -p --lang -l --comment --interactive -i --yes -y --no-backup --plural-form --help -h"
    local delete_opts="--path -p --yes -y --no-backup --all-duplicates --help -h"
    local merge_duplicates_opts="--path -p --all --auto-first --yes -y --no-backup --help -h"
    local export_opts="--path -p --output -o --format --include-status --help -h"
    local import_opts="--path -p --overwrite --no-backup --help -h"
    local edit_opts="--path -p --source-path --no-backup --help -h"
    local translate_opts="--path -p --provider --target-languages --batch-size --only-missing --overwrite --dry-run --no-cache --no-backup --source-language --format --config-file --help -h"
    local config_opts="set-api-key get-api-key delete-api-key list-providers --help -h"
    local scan_opts="--path -p --source-path --file --exclude --strict --show-unused --show-missing --show-references --resource-classes --localization-methods --format --help -h"
    local check_opts="--path -p --source-path --exclude --strict --format --help -h"
    local chain_opts="--continue-on-error --dry-run --help -h"
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

    # Web command options
    local web_opts="--path -p --source-path --port --bind-address --no-open-browser --enable-https --cert-path --cert-password --help -h"

    # Cloud subcommands and options
    local cloud_opts="init clone login logout status push pull log revert snapshot set-token set-api-key remote --help -h"
    local cloud_init_opts="--path -p --name -n --organization --yes -y --help -h"
    local cloud_clone_opts="--email --password --api-key --no-pull --force --help -h"
    local cloud_login_opts="--path -p --email --password --help -h"
    local cloud_logout_opts="--path -p --help -h"
    local cloud_status_opts="--path -p --format --account --help -h"
    local cloud_push_opts="--path -p --message -m --dry-run --force --config-only --resources-only --help -h"
    local cloud_pull_opts="--path -p --dry-run --force --no-backup --strategy --config-only --resources-only --help -h"
    local cloud_log_opts="--path -p --format -f --number -n --page --oneline --help -h"
    local cloud_revert_opts="--path -p --format -f --message -m --yes -y --dry-run --help -h"
    local cloud_snapshot_opts="list create show restore delete diff --help -h"
    local cloud_snapshot_list_opts="--path -p --format -f --page --page-size --help -h"
    local cloud_snapshot_create_opts="--path -p --format -f --help -h"
    local cloud_set_token_opts="--path -p --expires --help -h"
    local cloud_set_api_key_opts="--path -p --remove --help -h"
    local cloud_remote_opts="set get unset --help -h"

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
        --backend)
            # Complete backend formats
            COMPREPLY=( $(compgen -W "json resx" -- "${cur}") )
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
        --culture|-c|--copy-from|--cultures)
            # Suggest language codes
            COMPREPLY=( $(compgen -W "en es fr de it pt ja zh ko ru ar el tr nl pl cs sv" -- "${cur}") )
            return 0
            ;;
        --search-in)
            # Suggest search scopes
            COMPREPLY=( $(compgen -W "keys values comments both all" -- "${cur}") )
            return 0
            ;;
        --status)
            # Suggest translation statuses
            COMPREPLY=( $(compgen -W "translated untranslated empty all" -- "${cur}") )
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
            # Complete based on command: .resx files for backup, source files for scan
            if [[ "${command}" == "scan" ]]; then
                # Complete source files (.cs, .razor, .xaml)
                COMPREPLY=( $(compgen -f -X '!*.@(cs|razor|xaml|cshtml)' -- "${cur}") )
            else
                # Complete .resx files for backup commands
                COMPREPLY=( $(compgen -f -X '!*.resx' -- "${cur}") )
            fi
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
        init)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${init_opts}" -- "${cur}") )
            elif [[ "${prev}" == "--format" ]]; then
                COMPREPLY=( $(compgen -W "json resx" -- "${cur}") )
            else
                COMPREPLY=()
            fi
            ;;
        convert)
            if [[ "${cur}" == -* ]]; then
                COMPREPLY=( $(compgen -W "${convert_opts}" -- "${cur}") )
            elif [[ "${prev}" == "--from" || "${prev}" == "--to" ]]; then
                COMPREPLY=( $(compgen -W "json resx" -- "${cur}") )
            else
                COMPREPLY=()
            fi
            ;;
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
        chain)
            COMPREPLY=( $(compgen -W "${chain_opts}" -- "${cur}") )
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
        web)
            COMPREPLY=( $(compgen -W "${web_opts}" -- "${cur}") )
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
        cloud)
            # Handle cloud subcommands
            if [[ -z "${subcommand}" ]]; then
                # No subcommand yet, suggest subcommands
                COMPREPLY=( $(compgen -W "${cloud_opts}" -- "${cur}") )
            else
                # Complete options for the specific subcommand
                case "${subcommand}" in
                    init)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_init_opts}" -- "${cur}") )
                        else
                            # URL argument
                            COMPREPLY=()
                        fi
                        ;;
                    clone)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_clone_opts}" -- "${cur}") )
                        else
                            # URL argument or path argument
                            COMPREPLY=( $(compgen -d -- "${cur}") )
                        fi
                        ;;
                    login)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_login_opts}" -- "${cur}") )
                        else
                            # Host argument
                            COMPREPLY=()
                        fi
                        ;;
                    logout)
                        COMPREPLY=( $(compgen -W "${cloud_logout_opts}" -- "${cur}") )
                        ;;
                    status)
                        COMPREPLY=( $(compgen -W "${cloud_status_opts}" -- "${cur}") )
                        ;;
                    push)
                        COMPREPLY=( $(compgen -W "${cloud_push_opts}" -- "${cur}") )
                        ;;
                    pull)
                        COMPREPLY=( $(compgen -W "${cloud_pull_opts}" -- "${cur}") )
                        ;;
                    set-token)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_set_token_opts}" -- "${cur}") )
                        else
                            # Token argument
                            COMPREPLY=()
                        fi
                        ;;
                    set-api-key)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_set_api_key_opts}" -- "${cur}") )
                        else
                            # API key argument
                            COMPREPLY=()
                        fi
                        ;;
                    log)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_log_opts}" -- "${cur}") )
                        else
                            # History ID argument
                            COMPREPLY=()
                        fi
                        ;;
                    revert)
                        if [[ "${cur}" == -* ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_revert_opts}" -- "${cur}") )
                        else
                            # History ID argument
                            COMPREPLY=()
                        fi
                        ;;
                    snapshot)
                        # Check for third level subcommand
                        local snapshot_subcommand=""
                        for ((j=i+1; j<COMP_CWORD; j++)); do
                            if [[ "${COMP_WORDS[j]}" != -* ]]; then
                                snapshot_subcommand="${COMP_WORDS[j]}"
                                break
                            fi
                        done
                        if [[ -z "${snapshot_subcommand}" ]]; then
                            COMPREPLY=( $(compgen -W "${cloud_snapshot_opts}" -- "${cur}") )
                        else
                            case "${snapshot_subcommand}" in
                                list)
                                    COMPREPLY=( $(compgen -W "${cloud_snapshot_list_opts}" -- "${cur}") )
                                    ;;
                                create)
                                    COMPREPLY=( $(compgen -W "${cloud_snapshot_create_opts}" -- "${cur}") )
                                    ;;
                                show|restore|delete)
                                    # Snapshot ID argument
                                    COMPREPLY=()
                                    ;;
                                diff)
                                    # From/To snapshot ID arguments
                                    COMPREPLY=()
                                    ;;
                                *)
                                    COMPREPLY=()
                                    ;;
                            esac
                        fi
                        ;;
                    remote)
                        COMPREPLY=( $(compgen -W "${cloud_remote_opts}" -- "${cur}") )
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
